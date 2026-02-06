namespace QuanTAlib;

public class VortexTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vortex.Update(bars[i]);
        }

        Assert.True(double.IsFinite(vortex.Last.Value));
        Assert.True(double.IsFinite(vortex.ViPlus.Value));
        Assert.True(double.IsFinite(vortex.ViMinus.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            vortex.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        vortex.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        var val2 = vortex.Update(modifiedBar, false);
        var viPlus2 = vortex.ViPlus.Value;
        var viMinus2 = vortex.ViMinus.Value;

        // Create new instance and feed up to modified
        var vortex2 = new Vortex(14);
        for (int i = 0; i < 99; i++)
        {
            vortex2.Update(bars[i]);
        }
        var val3 = vortex2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
        Assert.Equal(vortex2.ViPlus.Value, viPlus2, 1e-9);
        Assert.Equal(vortex2.ViMinus.Value, viMinus2, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vortex.Update(bars[i]);
        }

        vortex.Reset();
        Assert.Equal(0, vortex.Last.Value);
        Assert.Equal(0, vortex.ViPlus.Value);
        Assert.Equal(0, vortex.ViMinus.Value);
        Assert.False(vortex.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            vortex.Update(bars[i]);
        }

        Assert.True(double.IsFinite(vortex.Last.Value));
        Assert.True(double.IsFinite(vortex.ViPlus.Value));
        Assert.True(double.IsFinite(vortex.ViMinus.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingViPlus = new List<double>();
        var streamingViMinus = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            vortex.Update(bars[i]);
            streamingViPlus.Add(vortex.ViPlus.Value);
            streamingViMinus.Add(vortex.ViMinus.Value);
        }

        var vortex2 = new Vortex(14);
        var seriesResults = vortex2.Update(bars);

        Assert.Equal(streamingViPlus.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingViPlus[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var staticResults = Vortex.Batch(bars, 14);

        Assert.Equal(bars.Count, staticResults.Count);

        // Verify that after warmup, values are finite and reasonable
        for (int i = 14; i < staticResults.Count; i++)
        {
            Assert.True(double.IsFinite(staticResults.Values[i]));
        }
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Vortex(0));
        Assert.Throws<ArgumentException>(() => new Vortex(1));
        Assert.Throws<ArgumentException>(() => new Vortex(-1));
    }

    [Fact]
    public void ManualCalculation_Verify()
    {
        // Simple manual test with period = 2
        // Create bars manually

        var vortex = new Vortex(2);

        // Bar 0: O=100, H=105, L=95, C=102, V=1000
        var bar0 = new TBar(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 100, 105, 95, 102, 1000);
        vortex.Update(bar0);
        // First bar: no previous bar, VI+ = VI- = 0
        Assert.Equal(0, vortex.ViPlus.Value);
        Assert.Equal(0, vortex.ViMinus.Value);

        // Bar 1: O=102, H=110, L=98, C=108
        var bar1 = new TBar(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000, 102, 110, 98, 108, 1000);
        vortex.Update(bar1);
        // VM+ = |H1 - L0| = |110 - 95| = 15
        // VM- = |L1 - H0| = |98 - 105| = 7
        // TR = max(H1-L1, |H1-C0|, |L1-C0|) = max(12, |110-102|, |98-102|) = max(12, 8, 4) = 12
        // Only 1 sample in buffer (not full yet with period=2)
        Assert.Equal(0, vortex.ViPlus.Value);  // Not IsHot yet
        Assert.Equal(0, vortex.ViMinus.Value);

        // Bar 2: O=108, H=115, L=100, C=112
        var bar2 = new TBar(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 2000, 108, 115, 100, 112, 1000);
        vortex.Update(bar2);
        // VM+ = |H2 - L1| = |115 - 98| = 17
        // VM- = |L2 - H1| = |100 - 110| = 10
        // TR = max(H2-L2, |H2-C1|, |L2-C1|) = max(15, |115-108|, |100-108|) = max(15, 7, 8) = 15
        // Sum(VM+) = 15 + 17 = 32
        // Sum(VM-) = 7 + 10 = 17
        // Sum(TR) = 12 + 15 = 27
        // VI+ = 32 / 27 ≈ 1.185
        // VI- = 17 / 27 ≈ 0.630

        Assert.True(vortex.IsHot);
        Assert.True(Math.Abs(vortex.ViPlus.Value - 32.0 / 27.0) < 0.001);
        Assert.True(Math.Abs(vortex.ViMinus.Value - 17.0 / 27.0) < 0.001);
    }

    [Fact]
    public void OutputsValuesAroundOne()
    {
        // Vortex indicator typically oscillates around 1.0
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vortex.Update(bars[i]);
        }

        // VI+ and VI- typically range between 0.5 and 1.5
        Assert.True(vortex.ViPlus.Value > 0 && vortex.ViPlus.Value < 3);
        Assert.True(vortex.ViMinus.Value > 0 && vortex.ViMinus.Value < 3);
    }

    [Fact]
    public void Uptrend_ViPlusGreaterThanViMinus()
    {
        var vortex = new Vortex(14);

        // Create strong uptrend data
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        double basePrice = 100;

        for (int i = 0; i < 50; i++)
        {
            double price = basePrice + i * 2; // Strong uptrend
            var bar = new TBar(baseTime + i * 60000, price, price + 1, price - 0.5, price + 0.5, 1000);
            vortex.Update(bar);
        }

        // In a strong uptrend, VI+ should be greater than VI-
        Assert.True(vortex.ViPlus.Value > vortex.ViMinus.Value);
    }

    [Fact]
    public void Downtrend_ViMinusGreaterThanViPlus()
    {
        var vortex = new Vortex(14);

        // Create strong downtrend data
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        double basePrice = 200;

        for (int i = 0; i < 50; i++)
        {
            double price = basePrice - i * 2; // Strong downtrend
            var bar = new TBar(baseTime + i * 60000, price, price + 0.5, price - 1, price - 0.5, 1000);
            vortex.Update(bar);
        }

        // In a strong downtrend, VI- should be greater than VI+
        Assert.True(vortex.ViMinus.Value > vortex.ViPlus.Value);
    }

    [Fact]
    public void WarmupPeriod_Correct()
    {
        var vortex = new Vortex(14);
        Assert.Equal(14, vortex.WarmupPeriod);
        Assert.Equal(14, vortex.Period);
    }

    [Fact]
    public void Name_ReflectsParameters()
    {
        var vortex = new Vortex(14);
        Assert.Equal("Vortex(14)", vortex.Name);

        var vortex2 = new Vortex(21);
        Assert.Equal("Vortex(21)", vortex2.Name);
    }

    [Fact]
    public void NaN_Input_Handles_Gracefully()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 30; i++)
        {
            vortex.Update(bars[i]);
        }

        // Inject NaN
        var nanBar = new TBar(bars[30].Time, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        vortex.Update(nanBar);

        Assert.True(double.IsFinite(vortex.ViPlus.Value));
        Assert.True(double.IsFinite(vortex.ViMinus.Value));
    }

    [Fact]
    public void Infinity_Input_Handles_Gracefully()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 30; i++)
        {
            vortex.Update(bars[i]);
        }

        // Inject Infinity
        var infBar = new TBar(bars[30].Time, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0);
        vortex.Update(infBar);

        Assert.True(double.IsFinite(vortex.ViPlus.Value));
        Assert.True(double.IsFinite(vortex.ViMinus.Value));
    }

    [Fact]
    public void Event_Publishes_Correctly()
    {
        var vortex = new Vortex(14);
        var eventCount = 0;
        TValue lastValue = default;
        bool wasPublished = false;

        vortex.Pub += (object? sender, in TValueEventArgs args) =>
        {
            eventCount++;
            lastValue = args.Value;
            wasPublished = true;
        };

        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vortex.Update(bars[i]);
        }

        Assert.Equal(20, eventCount);
        Assert.True(wasPublished);
    }

    [Fact]
    public void TValue_Update_Works()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var values = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        for (int i = 0; i < values.Count; i++)
        {
            vortex.Update(values[i]);
        }

        Assert.True(double.IsFinite(vortex.ViPlus.Value));
        Assert.True(double.IsFinite(vortex.ViMinus.Value));
    }

    [Fact]
    public void Last_EqualsViPlus()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vortex.Update(bars[i]);
            Assert.Equal(vortex.ViPlus.Value, vortex.Last.Value);
        }
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmup()
    {
        var vortex = new Vortex(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 14; i++)
        {
            vortex.Update(bars[i]);
            // First bar initializes, then period-1 more to fill buffer
            if (i < 14)
            {
                Assert.False(vortex.IsHot);
            }
        }

        vortex.Update(bars[14]);
        Assert.True(vortex.IsHot);
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var vortex14 = new Vortex(14);
        var vortex21 = new Vortex(21);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            vortex14.Update(bars[i]);
            vortex21.Update(bars[i]);
        }

        Assert.NotEqual(vortex14.ViPlus.Value, vortex21.ViPlus.Value);
        Assert.NotEqual(vortex14.ViMinus.Value, vortex21.ViMinus.Value);
    }
}
