
namespace QuanTAlib;

public class AoTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            ao.Update(bars[i]);
        }

        Assert.True(double.IsFinite(ao.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            ao.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        ao.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = ao.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var ao2 = new Ao(5, 34);
        for (int i = 0; i < 99; i++)
        {
            ao2.Update(bars[i]);
        }
        var val3 = ao2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            ao.Update(bars[i]);
        }

        ao.Reset();
        Assert.Equal(0, ao.Last.Value);
        Assert.False(ao.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            ao.Update(bars[i]);
        }

        Assert.True(double.IsFinite(ao.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(ao.Update(bars[i]).Value);
        }

        var ao2 = new Ao(5, 34);
        var seriesResults = ao2.Update(bars);

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

        var ao = new Ao(5, 34);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(ao.Update(bars[i]).Value);
        }

        var staticResults = Ao.Batch(bars, 5, 34);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TBarSeries chain
        var result = ao.Update(bars);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TBar chain (returns TValue)
        var result2 = ao.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Ao(0, 34));
        Assert.Throws<ArgumentException>(() => new Ao(5, 0));
        Assert.Throws<ArgumentException>(() => new Ao(34, 5)); // Fast >= Slow
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 50 new values (more than slow period)
        TBar fiftiethInput = default;
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            fiftiethInput = bar;
            ao.Update(bar, isNew: true);
        }

        // Remember state after 50 values
        double stateAfterFifty = ao.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            ao.Update(bar, isNew: false);
        }

        // Feed the remembered 50th input again with isNew=false
        TValue finalResult = ao.Update(fiftiethInput, isNew: false);

        // State should match the original state after 50 values
        Assert.Equal(stateAfterFifty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();

        Assert.False(ao.IsHot);

        // Feed bars until IsHot becomes true
        int count = 0;
        while (!ao.IsHot && count < 100)
        {
            var bar = gbm.Next(isNew: true);
            ao.Update(bar, isNew: true);
            count++;
        }

        Assert.True(ao.IsHot);
        Assert.True(count >= 34); // Should take at least slow period bars
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 40; i++)
        {
            ao.Update(bars[i]);
        }

        // Create a bar with NaN values
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = ao.Update(nanBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 40; i++)
        {
            ao.Update(bars[i]);
        }

        // Create a bar with Infinity values
        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = ao.Update(infBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int fastPeriod = 5;
        int slowPeriod = 34;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode (static method)
        var batchSeries = Ao.Batch(bars, fastPeriod, slowPeriod);
        double expected = batchSeries.Last.Value;

        // 2. Streaming Mode (instance, one bar at a time)
        var streamingInd = new Ao(fastPeriod, slowPeriod);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingInd.Update(bars[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. Instance Update with TBarSeries
        var instanceInd = new Ao(fastPeriod, slowPeriod);
        var instanceResult = instanceInd.Update(bars);
        double instanceValue = instanceResult.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, instanceValue, precision: 9);
    }
}
