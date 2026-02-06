
namespace QuanTAlib;

public class DxTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dx.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dx.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            dx.Update(bars[i]);
        }

        // Update with 100th point (isNew=true is default, so omit it)
        dx.Update(bars[99]);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = dx.Update(modifiedBar, isNew: false);

        // Create new instance and feed up to modified
        var dx2 = new Dx(14);
        for (int i = 0; i < 99; i++)
        {
            dx2.Update(bars[i]);
        }
        var val3 = dx2.Update(modifiedBar);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
        Assert.Equal(dx2.DiPlus.Value, dx.DiPlus.Value, 1e-9);
        Assert.Equal(dx2.DiMinus.Value, dx.DiMinus.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 50; i++)
        {
            dx.Update(bars[i]);
        }

        var originalValue = dx.Last;

        for (int m = 0; m < 5; m++)
        {
            var modified = new TBar(bars[49].Time, bars[49].Open, bars[49].High + m, bars[49].Low - m, bars[49].Close, bars[49].Volume);
            dx.Update(modified, isNew: false);
        }

        var restored = dx.Update(bars[49], isNew: false);
        Assert.Equal(originalValue.Value, restored.Value, 9);
    }

    [Fact]
    public void Reset_Works()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dx.Update(bars[i]);
        }

        dx.Reset();
        Assert.Equal(0, dx.Last.Value);
        Assert.False(dx.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            dx.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dx.Last.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(dx.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            dx.Update(bars[i]);
            if (dx.IsHot)
            {
                break;
            }
        }

        Assert.True(dx.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 40; i++)
        {
            dx.Update(bars[i]);
        }

        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 100);
        var result = dx.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 40; i++)
        {
            dx.Update(bars[i]);
        }

        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 0, 100, 100);
        var result = dx.Update(infBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchResult = Dx.Batch(bars, 14);
        double expected = batchResult.Last.Value;

        // 2. Streaming Mode
        var streamDx = new Dx(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamDx.Update(bars[i]);
        }

        double streamResult = streamDx.Last.Value;

        Assert.Equal(expected, streamResult, 9);
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(dx.Update(bars[i]).Value);
        }

        var dx2 = new Dx(14);
        var seriesResults = dx2.Update(bars);

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

        var dx = new Dx(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(dx.Update(bars[i]).Value);
        }

        var staticResults = Dx.Batch(bars, 14);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TBarSeries chain
        var result = dx.Update(bars);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TBar chain (returns TValue)
        var result2 = dx.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Dx(0));
        Assert.Throws<ArgumentException>(() => new Dx(-1));
    }

    [Fact]
    public void DiPlus_DiMinus_AreValid()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dx.Update(bars[i]);
        }

        // +DI and -DI should be between 0 and 100
        Assert.InRange(dx.DiPlus.Value, 0, 100);
        Assert.InRange(dx.DiMinus.Value, 0, 100);
        Assert.InRange(dx.Last.Value, 0, 100);
    }

    [Fact]
    public void DX_Range_IsBetween0And100()
    {
        var dx = new Dx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            var result = dx.Update(bars[i]);
            if (dx.IsHot)
            {
                Assert.InRange(result.Value, 0, 100);
            }
        }
    }

    [Fact]
    public void DefaultPeriod_Is14()
    {
        var dx = new Dx();
        Assert.Equal(14, dx.Period);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var dx = new Dx(20);
        Assert.Equal(20, dx.WarmupPeriod);
    }
}
