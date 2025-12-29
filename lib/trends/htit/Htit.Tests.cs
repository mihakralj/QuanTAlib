
namespace QuanTAlib.Tests;

public class HtitTests
{
    private readonly GBM _gbm;

    public HtitTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmup()
    {
        var htit = new Htit();
        for (int i = 0; i < 12; i++)
        {
            Assert.False(htit.IsHot);
            htit.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        }
        Assert.True(htit.IsHot);
    }

    [Fact]
    public void Update_Matches_Calculate()
    {
        var htit = new Htit();
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var series = data;

        var resultSeries = htit.Update(series);

        // Reset and calculate streaming
        htit.Reset();
        var streamingResults = new List<double>();
        foreach (var item in data)
        {
            streamingResults.Add(htit.Update(item).Value);
        }

        for (int i = 0; i < resultSeries.Count; i++)
        {
            Assert.Equal(resultSeries.Values[i], streamingResults[i], 1e-9);
        }
    }

    [Fact]
    public void Calculate_Span_Matches_Update()
    {
        var htit = new Htit();
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var series = data;

        var resultSeries = htit.Update(series);

        var spanInput = data.Values.ToArray();
        var spanOutput = new double[spanInput.Length];

        Htit.Calculate(spanInput, spanOutput);

        for (int i = 0; i < resultSeries.Count; i++)
        {
            Assert.Equal(resultSeries.Values[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Handles_NaN()
    {
        var htit = new Htit();
        htit.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        htit.Update(new TValue(DateTime.UtcNow.Ticks, double.NaN));

        Assert.Equal(100.0, htit.Last.Value);
    }

    [Fact]
    public void Htit_Calc_IsNew_AcceptsParameter()
    {
        var htit = new Htit();
        htit.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, htit.Last.Value);
    }

    [Fact]
    public void Htit_Reset_ClearsState()
    {
        var htit = new Htit();
        htit.Update(new TValue(DateTime.UtcNow, 100));
        htit.Update(new TValue(DateTime.UtcNow, 110));

        htit.Reset();

        Assert.True(double.IsNaN(htit.Last.Value));
        Assert.False(htit.IsHot);
    }

    [Fact]
    public void Htit_IterativeCorrections_RestoreToOriginalState()
    {
        var htit = new Htit();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values (needs > 12 for warmup)
        TValue lastInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            htit.Update(lastInput, isNew: true);
        }

        // Remember state after 20 values
        double valueAfterTwenty = htit.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            htit.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 20th input again with isNew=false
        TValue finalValue = htit.Update(lastInput, isNew: false);

        // Should match the original state after 20 values
        Assert.Equal(valueAfterTwenty, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Htit_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Htit.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan()));
    }

    [Fact]
    public void Htit_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Htit.Calculate(source.AsSpan(), output.AsSpan());

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Htit_AllModes_ProduceSameResult()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Htit.Batch(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Htit.Calculate(spanInput, spanOutput);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Htit();
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Htit(pubSource);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
