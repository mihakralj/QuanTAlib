
namespace QuanTAlib;

public class AdxTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            adx.Update(bars[i]);
        }

        Assert.True(double.IsFinite(adx.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            adx.Update(bars[i]);
        }

        // Update with 100th point (isNew=true is default, so omit it)
        adx.Update(bars[99]);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = adx.Update(modifiedBar, isNew: false);

        // Create new instance and feed up to modified
        var adx2 = new Adx(14);
        for (int i = 0; i < 99; i++)
        {
            adx2.Update(bars[i]);
        }
        var val3 = adx2.Update(modifiedBar);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
        Assert.Equal(adx2.DiPlus.Value, adx.DiPlus.Value, 1e-9);
        Assert.Equal(adx2.DiMinus.Value, adx.DiMinus.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 50; i++)
            adx.Update(bars[i]);

        var originalValue = adx.Last;

        for (int m = 0; m < 5; m++)
        {
            var modified = new TBar(bars[49].Time, bars[49].Open, bars[49].High + m, bars[49].Low - m, bars[49].Close, bars[49].Volume);
            adx.Update(modified, isNew: false);
        }

        var restored = adx.Update(bars[49], isNew: false);
        Assert.Equal(originalValue.Value, restored.Value, 9);
    }

    [Fact]
    public void Reset_Works()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            adx.Update(bars[i]);
        }

        adx.Reset();
        Assert.Equal(0, adx.Last.Value);
        Assert.False(adx.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            adx.Update(bars[i]);
        }

        Assert.True(double.IsFinite(adx.Last.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(adx.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            adx.Update(bars[i]);
            if (adx.IsHot) break;
        }

        Assert.True(adx.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 40; i++)
            adx.Update(bars[i]);

        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 100);
        var result = adx.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 40; i++)
            adx.Update(bars[i]);

        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 0, 100, 100);
        var result = adx.Update(infBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchResult = Adx.Batch(bars, 14);
        double expected = batchResult.Last.Value;

        // 2. Streaming Mode
        var streamAdx = new Adx(14);
        for (int i = 0; i < bars.Count; i++)
            streamAdx.Update(bars[i]);
        double streamResult = streamAdx.Last.Value;

        Assert.Equal(expected, streamResult, 9);
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(adx.Update(bars[i]).Value);
        }

        var adx2 = new Adx(14);
        var seriesResults = adx2.Update(bars);

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

        var adx = new Adx(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(adx.Update(bars[i]).Value);
        }

        var staticResults = Adx.Batch(bars, 14);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var adx = new Adx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TBarSeries chain
        var result = adx.Update(bars);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TBar chain (returns TValue)
        var result2 = adx.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Adx(0));
        Assert.Throws<ArgumentException>(() => new Adx(-1));
    }
}
