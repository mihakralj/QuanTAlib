
namespace QuanTAlib.Tests;

public class AlmaTests
{
    [Fact]
    public void Alma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Alma(0));
        Assert.Throws<ArgumentException>(() => new Alma(10, sigma: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Alma(10, offset: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Alma(10, offset: 1.1));

        var alma = new Alma(10);
        Assert.NotNull(alma);
    }

    [Fact]
    public void Alma_Calc_ReturnsValue()
    {
        var alma = new Alma(10);
        TValue result = alma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Alma_IsHot_BecomesTrueWhenBufferFull()
    {
        var alma = new Alma(5);

        Assert.False(alma.IsHot);

        for (int i = 0; i < 4; i++)
        {
            alma.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(alma.IsHot);
        }

        alma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(alma.IsHot);
    }

    [Fact]
    public void Alma_StreamingMatchesBatch()
    {
        var almaStreaming = new Alma(10);
        var almaBatch = new Alma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streamingResults = new TSeries();
        Assert.True(series.Count > 0);
        foreach (var item in series)
        {
            streamingResults.Add(almaStreaming.Update(item));
        }

        // Batch
        var batchResults = almaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i].Value, batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Alma_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Alma(10).Update(series);
        var staticResults = Alma.Batch(series, 10);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Alma_SpanCalculate_MatchesSeries()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var seriesResults = Alma.Batch(series, 10);

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Alma.Calculate(input.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(seriesResults[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Alma_Update_IsNewFalse_CorrectsValue()
    {
        var alma = new Alma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            alma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Update with isNew=false (correction)
        var newBar = gbm.Next(isNew: true);
        alma.Update(new TValue(newBar.Time, newBar.Close), isNew: true);

        double valueAfterCommit = alma.Last.Value;

        // Now update the SAME bar with a different value
        alma.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);

        double valueAfterCorrection = alma.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Now restore original value
        alma.Update(new TValue(newBar.Time, newBar.Close), isNew: false);

        Assert.Equal(valueAfterCommit, alma.Last.Value, 1e-9);
    }

    [Fact]
    public void Alma_NaN_Input_UsesLastValidValue()
    {
        var alma = new Alma(5);

        alma.Update(new TValue(DateTime.UtcNow, 100));
        alma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = alma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Alma_Reset_ClearsState()
    {
        var alma = new Alma(10);
        alma.Update(new TValue(DateTime.UtcNow, 100));
        alma.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(alma.Last.Value > 0);

        alma.Reset();

        Assert.Equal(0, alma.Last.Value);
        Assert.False(alma.IsHot);
    }

    [Fact]
    public void Alma_FirstValue_ReturnsExpected()
    {
        var alma = new Alma(10);
        TValue result = alma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, result.Value, 1e-9);
    }

    [Fact]
    public void Alma_Properties_Accessible()
    {
        var alma = new Alma(10);
        Assert.False(alma.IsHot);
        Assert.Equal(0, alma.Last.Value);
    }

    [Fact]
    public void Alma_Calc_IsNew_AcceptsParameter()
    {
        var alma = new Alma(10);
        alma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, alma.Last.Value);
    }

    [Fact]
    public void Alma_IterativeCorrections_RestoreToOriginalState()
    {
        var alma = new Alma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            alma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = alma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            alma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = alma.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Alma_Infinity_Input_UsesLastValidValue()
    {
        var alma = new Alma(10);
        alma.Update(new TValue(DateTime.UtcNow, 100));
        alma.Update(new TValue(DateTime.UtcNow, 110));

        var resultPosInf = alma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = alma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void Alma_MultipleNaN_ContinuesWithLastValid()
    {
        var alma = new Alma(10);
        alma.Update(new TValue(DateTime.UtcNow, 100));

        var r1 = alma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = alma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void Alma_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Alma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Alma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Alma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Alma(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }

    [Fact]
    public void Alma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Alma.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Alma.Calculate(source.AsSpan(), output.AsSpan(), 3, sigma: 0));
        Assert.Throws<ArgumentException>(() => Alma.Calculate(source.AsSpan(), output.AsSpan(), 3, sigma: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Alma.Calculate(source.AsSpan(), output.AsSpan(), 3, offset: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Alma.Calculate(source.AsSpan(), output.AsSpan(), 3, offset: 1.1));
        Assert.Throws<ArgumentException>(() => Alma.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Alma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Alma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }
}
