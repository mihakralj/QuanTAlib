
namespace QuanTAlib;

public class DmhTests
{
    [Fact]
    public void Constructor_InvalidParameters_ThrowsException()
    {
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dmh(0));
        Assert.Contains("period", ex1.Message, StringComparison.OrdinalIgnoreCase);

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dmh(-1));
        Assert.Contains("period", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ValidPeriod_NoThrow()
    {
        var dmh = new Dmh(1);
        Assert.Equal("Dmh(1)", dmh.Name);

        var dmh14 = new Dmh(14);
        Assert.Equal("Dmh(14)", dmh14.Name);
        Assert.Equal(15, dmh14.WarmupPeriod);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dmh.Last.Value));
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmup()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < dmh.WarmupPeriod - 1; i++)
        {
            dmh.Update(bars[i]);
            Assert.False(dmh.IsHot, $"Should not be hot at bar {i}");
        }

        dmh.Update(bars[dmh.WarmupPeriod - 1]);
        Assert.True(dmh.IsHot, "Should be hot after warmup");
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 99; i++)
        {
            dmh.Update(bars[i]);
        }

        dmh.Update(bars[99], true);

        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = dmh.Update(modifiedBar, false);

        var dmh2 = new Dmh(14);
        for (int i = 0; i < 99; i++)
        {
            dmh2.Update(bars[i]);
        }
        var val3 = dmh2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 50; i++)
        {
            dmh.Update(bars[i]);
        }

        var originalValue = dmh.Last;

        for (int m = 0; m < 5; m++)
        {
            var modified = new TBar(bars[49].Time, bars[49].Open, bars[49].High + m, bars[49].Low - m, bars[49].Close, bars[49].Volume);
            dmh.Update(modified, isNew: false);
        }

        var restored = dmh.Update(bars[49], isNew: false);
        Assert.Equal(originalValue.Value, restored.Value, 9);
    }

    [Fact]
    public void Reset_Works()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        dmh.Reset();
        Assert.Equal(0, dmh.Last.Value);
        Assert.False(dmh.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dmh.Last.Value));
        Assert.True(dmh.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 30; i++)
        {
            dmh.Update(bars[i]);
        }

        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 100);
        var result = dmh.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 30; i++)
        {
            dmh.Update(bars[i]);
        }

        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 0, 100, 100);
        var result = dmh.Update(infBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchResult = Dmh.Batch(bars, 14);
        double expected = batchResult.Last.Value;

        // 2. Streaming Mode
        var streamDmh = new Dmh(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamDmh.Update(bars[i]);
        }

        double streamResult = streamDmh.Last.Value;

        Assert.Equal(expected, streamResult, 9);
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var dmh = new Dmh(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(dmh.Update(bars[i]).Value);
        }

        var dmh2 = new Dmh(14);
        var seriesResults = dmh2.Update(bars);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void FirstBar_Handling()
    {
        var dmh = new Dmh(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        var result = dmh.Update(bar);

        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmh = new Dmh(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(dmh.Update(bars[i]).Value);
        }

        var staticResults = Dmh.Batch(bars, 14);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var dmh = new Dmh(14);
        var sma = new Sma(dmh, 10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.True(double.IsFinite(sma.Last.Value));
    }

    [Fact]
    public void Uptrend_Produces_Positive_Values()
    {
        var dmh = new Dmh(14);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        double price = 100;

        for (int i = 0; i < 50; i++)
        {
            bars.Add(time, price, price + 2, price - 1, price + 1, 1000);
            time = time.AddMinutes(1);
            price += 1.0;
        }

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.True(dmh.Last.Value > 0, $"DMH should be positive in uptrend, got {dmh.Last.Value}");
    }

    [Fact]
    public void Downtrend_Produces_Negative_Values()
    {
        var dmh = new Dmh(14);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        double price = 200;

        for (int i = 0; i < 50; i++)
        {
            bars.Add(time, price, price + 1, price - 2, price - 1, 1000);
            time = time.AddMinutes(1);
            price -= 1.0;
        }

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.True(dmh.Last.Value < 0, $"DMH should be negative in downtrend, got {dmh.Last.Value}");
    }

    [Fact]
    public void SpanBatch_LengthMismatch_Throws()
    {
        var high = new double[10];
        var low = new double[5];
        var dest = new double[10];

        Assert.Throws<ArgumentException>(() => Dmh.Batch(high, low, 14, dest));
    }

    [Fact]
    public void SpanBatch_InvalidPeriod_Throws()
    {
        var high = new double[10];
        var low = new double[10];
        var dest = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Dmh.Batch(high, low, 0, dest));
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmh5 = new Dmh(5);
        var dmh20 = new Dmh(20);

        for (int i = 0; i < bars.Count; i++)
        {
            dmh5.Update(bars[i]);
            dmh20.Update(bars[i]);
        }

        Assert.NotEqual(dmh5.Last.Value, dmh20.Last.Value);
    }

    [Fact]
    public void EventPub_Fires()
    {
        var dmh = new Dmh(14);
        int eventCount = 0;
        dmh.Pub += (object? _, in TValueEventArgs _e) => eventCount++;

        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void PrimePeriod_EqualsWarmupPeriod()
    {
        var dmh = new Dmh(14);
        Assert.Equal(15, dmh.WarmupPeriod);

        var dmh7 = new Dmh(7);
        Assert.Equal(8, dmh7.WarmupPeriod);
    }

    [Fact]
    public void Prime_Initializes_State()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmh1 = new Dmh(14);
        for (int i = 0; i < bars.Count; i++)
        {
            dmh1.Update(bars[i]);
        }

        var dmh2 = new Dmh(14);
        dmh2.Prime(bars);

        Assert.Equal(dmh1.Last.Value, dmh2.Last.Value, 1e-9);
        Assert.Equal(dmh1.IsHot, dmh2.IsHot);
    }

    [Fact]
    public void Calculate_Returns_Results_And_Indicator()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Dmh.Calculate(bars, 14);

        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void ConstantPrice_Produces_Zero()
    {
        var dmh = new Dmh(14);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(time, 100, 100, 100, 100, 1000);
            dmh.Update(bar);
            time = time.AddMinutes(1);
        }

        Assert.Equal(0.0, dmh.Last.Value, 1e-12);
    }

    [Fact]
    public void Period1_Works()
    {
        var dmh = new Dmh(1);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dmh.Last.Value));
    }

    [Fact]
    public void LargePeriod_Works()
    {
        var dmh = new Dmh(200);
        var gbm = new GBM();
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmh.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dmh.Last.Value));
    }
}
