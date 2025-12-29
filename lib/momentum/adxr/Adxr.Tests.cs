
namespace QuanTAlib;

public class AdxrTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            adxr.Update(bars[i]);
        }

        Assert.True(double.IsFinite(adxr.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            adxr.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        adxr.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = adxr.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var adxr2 = new Adxr(14);
        for (int i = 0; i < 99; i++)
        {
            adxr2.Update(bars[i]);
        }
        var val3 = adxr2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            adxr.Update(bars[i]);
        }

        adxr.Reset();
        Assert.Equal(0, adxr.Last.Value);
        Assert.False(adxr.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            adxr.Update(bars[i]);
        }

        Assert.True(double.IsFinite(adxr.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(adxr.Update(bars[i]).Value);
        }

        var adxr2 = new Adxr(14);
        var seriesResults = adxr2.Update(bars);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticCalculate_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var adxr = new Adxr(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(adxr.Update(bars[i]).Value);
        }

        var staticResults = Adxr.Batch(bars, 14);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Adxr(0));
        Assert.Throws<ArgumentException>(() => new Adxr(-1));
    }

    [Fact]
    public void Chainability_Works()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TBarSeries chain
        var result = adxr.Update(bars);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TBar chain (returns TValue)
        var result2 = adxr.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var adxr = new Adxr(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values
        TBar twentiethInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentiethInput = bar;
            adxr.Update(bar, isNew: true);
        }

        // Remember state after 20 values
        double stateAfterTwenty = adxr.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            adxr.Update(bar, isNew: false);
        }

        // Feed the remembered 20th input again with isNew=false
        TValue finalResult = adxr.Update(twentiethInput, isNew: false);

        // State should match the original state after 20 values
        Assert.Equal(stateAfterTwenty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var adxr = new Adxr(5);
        var gbm = new GBM();

        Assert.False(adxr.IsHot);

        // ADXR needs more warmup than just period (ADX warmup + period)
        // Feed bars until IsHot becomes true
        int count = 0;
        while (!adxr.IsHot && count < 100)
        {
            var bar = gbm.Next(isNew: true);
            adxr.Update(bar, isNew: true);
            count++;
        }

        Assert.True(adxr.IsHot);
        Assert.True(count > 5); // Should take more than period bars
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var adxr = new Adxr(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 25; i++)
        {
            adxr.Update(bars[i]);
        }

        // Create a bar with NaN values
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = adxr.Update(nanBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var adxr = new Adxr(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 25; i++)
        {
            adxr.Update(bars[i]);
        }

        // Create a bar with Infinity values
        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = adxr.Update(infBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 5;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode (static method)
        var batchSeries = Adxr.Batch(bars, period);
        double expected = batchSeries.Last.Value;

        // 2. Streaming Mode (instance, one bar at a time)
        var streamingInd = new Adxr(period);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingInd.Update(bars[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. Instance Update with TBarSeries
        var instanceInd = new Adxr(period);
        var instanceResult = instanceInd.Update(bars);
        double instanceValue = instanceResult.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, instanceValue, precision: 9);
    }
}
