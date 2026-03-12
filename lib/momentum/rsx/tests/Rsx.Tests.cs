
namespace QuanTAlib;

public class RsxTests
{
    private readonly GBM _gbm;

    public RsxTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Rsx(0));
        Assert.Throws<ArgumentException>(() => new Rsx(-1));
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var rsx = new Rsx(14);
        var result = rsx.Update(new TValue(DateTime.UtcNow, 100));
        Assert.InRange(result.Value, 0, 100);
    }

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var rsx = new Rsx(14);
        rsx.Update(new TValue(DateTime.UtcNow, 100));
        var result = rsx.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Should not be NaN
        Assert.False(double.IsNaN(result.Value));
        Assert.InRange(result.Value, 0, 100);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var rsx = new Rsx(14);
        var time = DateTime.UtcNow;

        // Update with isNew=true
        var val1 = rsx.Update(new TValue(time, 100), true);

        // Update with isNew=false (same time, different value)
        rsx.Update(new TValue(time, 105), false);

        // Update with isNew=false (same time, original value) - should match val1 if state rollback works
        var val3 = rsx.Update(new TValue(time, 100), false);

        Assert.Equal(val1.Value, val3.Value, 1e-9);
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        const int period = 14;
        int count = 100;
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var rsx = new Rsx(period);

        var streamingResults = new List<double>();
        for (int i = 0; i < count; i++)
        {
            streamingResults.Add(rsx.Update(new TValue(series.Times[i], series.Values[i])).Value);
        }

        var staticResults = Rsx.Batch(series, period);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_Matches_Streaming()
    {
        int period = 14;
        int count = 100;
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var rsx = new Rsx(period);

        var streamingResults = new List<double>();
        for (int i = 0; i < count; i++)
        {
            streamingResults.Add(rsx.Update(new TValue(series.Times[i], series.Values[i])).Value);
        }

        var spanInput = series.Values.ToArray();
        var spanOutput = new double[count];
        Rsx.Batch(spanInput, spanOutput, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Reset_Works()
    {
        var rsx = new Rsx(14);
        rsx.Update(new TValue(DateTime.UtcNow, 100));
        rsx.Reset();

        // After reset, it should behave like a new instance
        var val1 = rsx.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(50.0, val1.Value); // Neutral start
    }

    [Fact]
    public void Chainability_Works()
    {
        var rsx = new Rsx(14);
        var rsx2 = new Rsx(rsx, 14);

        var result = rsx2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(double.IsNaN(result.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var rsx = new Rsx(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values
        TValue twentiethInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentiethInput = new TValue(bar.Time, bar.Close);
            rsx.Update(twentiethInput, isNew: true);
        }

        // Remember state after 20 values
        double stateAfterTwenty = rsx.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            rsx.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 20th input again with isNew=false
        TValue finalResult = rsx.Update(twentiethInput, isNew: false);

        // State should match the original state after 20 values
        Assert.Equal(stateAfterTwenty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterFirstValue()
    {
        var rsx = new Rsx(5);

        Assert.False(rsx.IsHot);

        // RSX uses IsInitialized for IsHot, which becomes true after first value
        rsx.Update(new TValue(DateTime.UtcNow, 100), isNew: true);

        Assert.True(rsx.IsHot);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var rsx = new Rsx(14);
        rsx.Update(new TValue(DateTime.UtcNow, 100));
        rsx.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = rsx.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.False(double.IsNaN(resultAfterPosInf.Value));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));
        Assert.InRange(resultAfterPosInf.Value, 0, 100);

        var resultAfterNegInf = rsx.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.False(double.IsNaN(resultAfterNegInf.Value));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
        Assert.InRange(resultAfterNegInf.Value, 0, 100);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 14;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode (static method)
        var batchSeries = Rsx.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode (static method with spans)
        var spanInput = series.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        Rsx.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Rsx(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode (chained via ITValuePublisher)
        var pubSource = new TSeries();
        var eventingInd = new Rsx(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
