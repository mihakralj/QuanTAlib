
namespace QuanTAlib;

public class ApoTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            apo.Update(bars[i]);
        }

        Assert.True(double.IsFinite(apo.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            apo.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        apo.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = apo.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var apo2 = new Apo(12, 26);
        for (int i = 0; i < 99; i++)
        {
            apo2.Update(bars[i]);
        }
        var val3 = apo2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            apo.Update(bars[i]);
        }

        apo.Reset();
        Assert.Equal(0, apo.Last.Value);
        Assert.False(apo.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            apo.Update(bars[i]);
        }

        Assert.True(double.IsFinite(apo.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(apo.Update(bars[i]).Value);
        }

        var apo2 = new Apo(12, 26);
        var seriesResults = apo2.Update(bars.Close);

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

        var apo = new Apo(12, 26);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(apo.Update(bars[i]).Value);
        }

        var staticResults = Apo.Batch(bars.Close, 12, 26);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TBarSeries chain
        var result = apo.Update(bars.Close);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TBar chain (returns TValue)
        var result2 = apo.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Apo(0, 26));
        Assert.Throws<ArgumentException>(() => new Apo(12, 0));
        Assert.Throws<ArgumentException>(() => new Apo(26, 12)); // Fast >= Slow
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 50 new values (more than slow period)
        TBar fiftiethInput = default;
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            fiftiethInput = bar;
            apo.Update(bar, isNew: true);
        }

        // Remember state after 50 values
        double stateAfterFifty = apo.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            apo.Update(bar, isNew: false);
        }

        // Feed the remembered 50th input again with isNew=false
        TValue finalResult = apo.Update(fiftiethInput, isNew: false);

        // State should match the original state after 50 values
        Assert.Equal(stateAfterFifty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();

        Assert.False(apo.IsHot);

        // Feed bars until IsHot becomes true
        int count = 0;
        while (!apo.IsHot && count < 100)
        {
            var bar = gbm.Next(isNew: true);
            apo.Update(bar, isNew: true);
            count++;
        }

        Assert.True(apo.IsHot);
        Assert.True(count >= 26); // Should take at least slow period bars
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 40; i++)
        {
            apo.Update(bars[i]);
        }

        // Create a bar with NaN close value
        var nanBar = new TBar(DateTime.UtcNow, 100, 105, 95, double.NaN, 1000);
        var result = apo.Update(nanBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 40; i++)
        {
            apo.Update(bars[i]);
        }

        // Create a bar with Infinity close value
        var infBar = new TBar(DateTime.UtcNow, 100, 105, 95, double.PositiveInfinity, 1000);
        var result = apo.Update(infBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int fastPeriod = 12;
        int slowPeriod = 26;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var closeSeries = bars.Close;

        // 1. Batch Mode (static method)
        var batchSeries = Apo.Batch(closeSeries, fastPeriod, slowPeriod);
        double expected = batchSeries.Last.Value;

        // 2. Streaming Mode (instance, one bar at a time)
        var streamingInd = new Apo(fastPeriod, slowPeriod);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingInd.Update(bars[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. Instance Update with TSeries
        var instanceInd = new Apo(fastPeriod, slowPeriod);
        var instanceResult = instanceInd.Update(closeSeries);
        double instanceValue = instanceResult.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, instanceValue, precision: 9);
    }
}
