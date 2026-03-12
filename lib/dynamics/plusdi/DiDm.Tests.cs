
namespace QuanTAlib;

/// <summary>
/// Combined unit tests for PlusDi, MinusDi, PlusDm, MinusDm.
/// All four are thin Dx-composition wrappers extracting a single property.
/// </summary>
public class DiDmTests
{
    // ═══════════════════════════════════════════════
    //  A. Constructor / Parameter Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_Constructor_InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlusDi(0));
        Assert.Throws<ArgumentException>(() => new PlusDi(-1));
    }

    [Fact]
    public void MinusDi_Constructor_InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MinusDi(0));
        Assert.Throws<ArgumentException>(() => new MinusDi(-1));
    }

    [Fact]
    public void PlusDm_Constructor_InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlusDm(0));
        Assert.Throws<ArgumentException>(() => new PlusDm(-1));
    }

    [Fact]
    public void MinusDm_Constructor_InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MinusDm(0));
        Assert.Throws<ArgumentException>(() => new MinusDm(-1));
    }

    [Fact]
    public void PlusDi_DefaultPeriod_Is14()
    {
        var indicator = new PlusDi();
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void MinusDi_DefaultPeriod_Is14()
    {
        var indicator = new MinusDi();
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void PlusDm_DefaultPeriod_Is14()
    {
        var indicator = new PlusDm();
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void MinusDm_DefaultPeriod_Is14()
    {
        var indicator = new MinusDm();
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void PlusDi_Name_ContainsPeriod()
    {
        var indicator = new PlusDi(20);
        Assert.Contains("20", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void MinusDi_Name_ContainsPeriod()
    {
        var indicator = new MinusDi(20);
        Assert.Contains("20", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void PlusDm_Name_ContainsPeriod()
    {
        var indicator = new PlusDm(20);
        Assert.Contains("20", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void MinusDm_Name_ContainsPeriod()
    {
        var indicator = new MinusDm(20);
        Assert.Contains("20", indicator.Name, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════
    //  B. Basic Calculation Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_BasicCalculation_DoesNotCrash()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void MinusDi_BasicCalculation_DoesNotCrash()
    {
        var indicator = new MinusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void PlusDm_BasicCalculation_DoesNotCrash()
    {
        var indicator = new PlusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void MinusDm_BasicCalculation_DoesNotCrash()
    {
        var indicator = new MinusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    // ═══════════════════════════════════════════════
    //  C. IsHot / WarmupPeriod Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                break;
            }
        }

        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void MinusDi_IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new MinusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                break;
            }
        }

        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void PlusDm_IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new PlusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                break;
            }
        }

        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void MinusDm_IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new MinusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                break;
            }
        }

        Assert.True(indicator.IsHot);
    }

    // ═══════════════════════════════════════════════
    //  D. Bar Correction (isNew=false) Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_BarCorrection_MatchesFreshInstance()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 99; i++)
        {
            indicator.Update(bars[i]);
        }
        indicator.Update(bars[99]);

        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = indicator.Update(modifiedBar, isNew: false);

        var fresh = new PlusDi(14);
        for (int i = 0; i < 99; i++)
        {
            fresh.Update(bars[i]);
        }
        var val3 = fresh.Update(modifiedBar);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void MinusDi_BarCorrection_MatchesFreshInstance()
    {
        var indicator = new MinusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 99; i++)
        {
            indicator.Update(bars[i]);
        }
        indicator.Update(bars[99]);

        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = indicator.Update(modifiedBar, isNew: false);

        var fresh = new MinusDi(14);
        for (int i = 0; i < 99; i++)
        {
            fresh.Update(bars[i]);
        }
        var val3 = fresh.Update(modifiedBar);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void PlusDm_BarCorrection_MatchesFreshInstance()
    {
        var indicator = new PlusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 99; i++)
        {
            indicator.Update(bars[i]);
        }
        indicator.Update(bars[99]);

        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = indicator.Update(modifiedBar, isNew: false);

        var fresh = new PlusDm(14);
        for (int i = 0; i < 99; i++)
        {
            fresh.Update(bars[i]);
        }
        var val3 = fresh.Update(modifiedBar);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void MinusDm_BarCorrection_MatchesFreshInstance()
    {
        var indicator = new MinusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 99; i++)
        {
            indicator.Update(bars[i]);
        }
        indicator.Update(bars[99]);

        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = indicator.Update(modifiedBar, isNew: false);

        var fresh = new MinusDm(14);
        for (int i = 0; i < 99; i++)
        {
            fresh.Update(bars[i]);
        }
        var val3 = fresh.Update(modifiedBar);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    // ═══════════════════════════════════════════════
    //  E. Reset Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_Reset_ClearsState()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        indicator.Reset();
        Assert.Equal(0, indicator.Last.Value);
        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void MinusDi_Reset_ClearsState()
    {
        var indicator = new MinusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        indicator.Reset();
        Assert.Equal(0, indicator.Last.Value);
        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void PlusDm_Reset_ClearsState()
    {
        var indicator = new PlusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        indicator.Reset();
        Assert.Equal(0, indicator.Last.Value);
        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void MinusDm_Reset_ClearsState()
    {
        var indicator = new MinusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        indicator.Reset();
        Assert.Equal(0, indicator.Last.Value);
        Assert.False(indicator.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    // ═══════════════════════════════════════════════
    //  F. Batch / Streaming Consistency Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_Batch_MatchesStreaming()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var batchResult = PlusDi.Batch(bars, 14);

        var streaming = new PlusDi(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streaming.Update(bars[i]);
        }

        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 9);
    }

    [Fact]
    public void MinusDi_Batch_MatchesStreaming()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var batchResult = MinusDi.Batch(bars, 14);

        var streaming = new MinusDi(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streaming.Update(bars[i]);
        }

        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 9);
    }

    [Fact]
    public void PlusDm_Batch_MatchesStreaming()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var batchResult = PlusDm.Batch(bars, 14);

        var streaming = new PlusDm(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streaming.Update(bars[i]);
        }

        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 9);
    }

    [Fact]
    public void MinusDm_Batch_MatchesStreaming()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var batchResult = MinusDm.Batch(bars, 14);

        var streaming = new MinusDm(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streaming.Update(bars[i]);
        }

        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 9);
    }

    // ═══════════════════════════════════════════════
    //  G. Event / Pub Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_Pub_FiresOnNewBar()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int fireCount = 0;

        indicator.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.Equal(bars.Count, fireCount);
    }

    [Fact]
    public void MinusDi_Pub_DoesNotFireOnCorrection()
    {
        var indicator = new MinusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int fireCount = 0;

        indicator.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        int countAfterNewBars = fireCount;

        // Bar correction should not fire
        var modifiedBar = new TBar(bars[^1].Time, bars[^1].Open, bars[^1].High + 1.0, bars[^1].Low - 1.0, bars[^1].Close, bars[^1].Volume);
        indicator.Update(modifiedBar, isNew: false);

        Assert.Equal(countAfterNewBars, fireCount);
    }

    [Fact]
    public void PlusDm_Pub_FiresOnNewBar()
    {
        var indicator = new PlusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int fireCount = 0;

        indicator.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        Assert.Equal(bars.Count, fireCount);
    }

    [Fact]
    public void MinusDm_Pub_DoesNotFireOnCorrection()
    {
        var indicator = new MinusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int fireCount = 0;

        indicator.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        int countAfterNewBars = fireCount;

        var modifiedBar = new TBar(bars[^1].Time, bars[^1].Open, bars[^1].High + 1.0, bars[^1].Low - 1.0, bars[^1].Close, bars[^1].Volume);
        indicator.Update(modifiedBar, isNew: false);

        Assert.Equal(countAfterNewBars, fireCount);
    }

    // ═══════════════════════════════════════════════
    //  H. Prime / Calculate / Chainability Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_Prime_SetsState()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        indicator.Prime(bars);

        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void MinusDi_Prime_SetsState()
    {
        var indicator = new MinusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        indicator.Prime(bars);

        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void PlusDm_Calculate_ReturnsTupleWithIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, ind) = PlusDm.Calculate(bars, 14);

        Assert.Equal(bars.Count, results.Count);
        Assert.True(ind.IsHot);
        Assert.Equal(results[^1].Value, ind.Last.Value, 1e-9);
    }

    [Fact]
    public void MinusDm_Calculate_ReturnsTupleWithIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, ind) = MinusDm.Calculate(bars, 14);

        Assert.Equal(bars.Count, results.Count);
        Assert.True(ind.IsHot);
        Assert.Equal(results[^1].Value, ind.Last.Value, 1e-9);
    }

    [Fact]
    public void PlusDi_TBarSeriesConstructor_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new PlusDi(bars, 14);

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void MinusDi_TBarSeriesConstructor_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new MinusDi(bars, 14);

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void PlusDi_ScalarUpdate_ReturnsLastUnchanged()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        var before = indicator.Last;
        var result = indicator.Update(new TValue(DateTime.UtcNow, 42.0));

        Assert.Equal(before.Value, result.Value);
    }

    [Fact]
    public void MinusDm_ScalarUpdate_ReturnsLastUnchanged()
    {
        var indicator = new MinusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            indicator.Update(bars[i]);
        }

        var before = indicator.Last;
        var result = indicator.Update(new TValue(DateTime.UtcNow, 42.0));

        Assert.Equal(before.Value, result.Value);
    }

    // ═══════════════════════════════════════════════
    //  Range / Value Constraint Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_OutputRange_0to100()
    {
        var indicator = new PlusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                Assert.InRange(result.Value, 0, 100);
            }
        }
    }

    [Fact]
    public void MinusDi_OutputRange_0to100()
    {
        var indicator = new MinusDi(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                Assert.InRange(result.Value, 0, 100);
            }
        }
    }

    [Fact]
    public void PlusDm_NonNegative()
    {
        var indicator = new PlusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                Assert.True(result.Value >= 0, $"+DM should be non-negative, got {result.Value}");
            }
        }
    }

    [Fact]
    public void MinusDm_NonNegative()
    {
        var indicator = new MinusDm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.Update(bars[i]);
            if (indicator.IsHot)
            {
                Assert.True(result.Value >= 0, $"-DM should be non-negative, got {result.Value}");
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  Dx Equivalence Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_MatchesDx_DiPlus()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var plusDi = new PlusDi(14);
        var dx = new Dx(14);

        for (int i = 0; i < bars.Count; i++)
        {
            plusDi.Update(bars[i]);
            dx.Update(bars[i]);

            Assert.Equal(dx.DiPlus.Value, plusDi.Last.Value, 1e-12);
        }
    }

    [Fact]
    public void MinusDi_MatchesDx_DiMinus()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var minusDi = new MinusDi(14);
        var dx = new Dx(14);

        for (int i = 0; i < bars.Count; i++)
        {
            minusDi.Update(bars[i]);
            dx.Update(bars[i]);

            Assert.Equal(dx.DiMinus.Value, minusDi.Last.Value, 1e-12);
        }
    }

    [Fact]
    public void PlusDm_MatchesDx_DmPlus()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var plusDm = new PlusDm(14);
        var dx = new Dx(14);

        for (int i = 0; i < bars.Count; i++)
        {
            plusDm.Update(bars[i]);
            dx.Update(bars[i]);

            Assert.Equal(dx.DmPlus.Value, plusDm.Last.Value, 1e-12);
        }
    }

    [Fact]
    public void MinusDm_MatchesDx_DmMinus()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var minusDm = new MinusDm(14);
        var dx = new Dx(14);

        for (int i = 0; i < bars.Count; i++)
        {
            minusDm.Update(bars[i]);
            dx.Update(bars[i]);

            Assert.Equal(dx.DmMinus.Value, minusDm.Last.Value, 1e-12);
        }
    }

    // ═══════════════════════════════════════════════
    //  Determinism Tests
    // ═══════════════════════════════════════════════

    [Fact]
    public void AllFour_Deterministic_SameSeedSameResult()
    {
        var gbm1 = new GBM(seed: 99);
        var bars1 = gbm1.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var gbm2 = new GBM(seed: 99);
        var bars2 = gbm2.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var pdi1 = new PlusDi(14);
        var pdi2 = new PlusDi(14);
        var mdi1 = new MinusDi(14);
        var mdi2 = new MinusDi(14);
        var pdm1 = new PlusDm(14);
        var pdm2 = new PlusDm(14);
        var mdm1 = new MinusDm(14);
        var mdm2 = new MinusDm(14);

        for (int i = 0; i < bars1.Count; i++)
        {
            pdi1.Update(bars1[i]);
            pdi2.Update(bars2[i]);
            mdi1.Update(bars1[i]);
            mdi2.Update(bars2[i]);
            pdm1.Update(bars1[i]);
            pdm2.Update(bars2[i]);
            mdm1.Update(bars1[i]);
            mdm2.Update(bars2[i]);
        }

        Assert.Equal(pdi1.Last.Value, pdi2.Last.Value, 1e-12);
        Assert.Equal(mdi1.Last.Value, mdi2.Last.Value, 1e-12);
        Assert.Equal(pdm1.Last.Value, pdm2.Last.Value, 1e-12);
        Assert.Equal(mdm1.Last.Value, mdm2.Last.Value, 1e-12);
    }
}
