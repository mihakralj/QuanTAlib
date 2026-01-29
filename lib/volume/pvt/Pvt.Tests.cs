using Xunit;

namespace QuanTAlib.Tests;

public class PvtTests
{
    private const double Tolerance = 1e-10;

    // ==================== Constructor Tests ====================

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        var pvt = new Pvt();

        Assert.Equal("Pvt", pvt.Name);
        Assert.Equal(0.0, pvt.Last.Value);
        Assert.False(pvt.IsHot);
        Assert.Equal(2, pvt.WarmupPeriod);
    }

    // ==================== Basic Calculation Tests ====================

    [Fact]
    public void Update_FirstBar_ReturnsZero()
    {
        var pvt = new Pvt();

        var result = pvt.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));

        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_SecondBar_PriceUp_ReturnsPositiveVolumeFraction()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));  // First bar, close=100
        var result = pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000));  // close=110, +10%

        // PVT = volume * (price_change / prev_price) = 2000 * (10/100) = 200
        Assert.Equal(200.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_SecondBar_PriceDown_ReturnsNegativeVolumeFraction()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));  // First bar, close=100
        var result = pvt.Update(new TBar(time.AddMinutes(1), 100, 100, 85, 90, 2000));  // close=90, -10%

        // PVT = volume * (price_change / prev_price) = 2000 * (-10/100) = -200
        Assert.Equal(-200.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_PriceUnchanged_NoChange()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));  // First bar
        var result = pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 90, 100, 5000));  // Same close

        // PVT = volume * (0/100) = 0
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_MultipleBars_AccumulatesCorrectly()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));  // First bar
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000));  // +10% -> +200
        pvt.Update(new TBar(time.AddMinutes(2), 110, 115, 105, 105, 1000));  // -4.545% from 110 -> ~-45.45
        var result = pvt.Update(new TBar(time.AddMinutes(3), 105, 120, 105, 120, 3000));  // +14.286% from 105 -> ~+428.57

        // Expected PVT:
        // Bar 1: 0
        // Bar 2: 0 + 2000 * (10/100) = 200
        // Bar 3: 200 + 1000 * (-5/110) = 200 - 45.4545... = 154.5454...
        // Bar 4: 154.5454 + 3000 * (15/105) = 154.5454 + 428.5714... = 583.1168...
        Assert.True(result.Value > 500 && result.Value < 600);  // Approximate check
    }

    [Fact]
    public void Update_SmallPriceChange_SmallPvtChange()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));  // First bar
        var result = pvt.Update(new TBar(time.AddMinutes(1), 100, 101, 99, 100.5, 10000));  // +0.5%

        // PVT = 10000 * (0.5/100) = 50
        Assert.Equal(50.0, result.Value, Tolerance);
    }

    // ==================== State Management Tests ====================

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        var value1 = pvt.Last.Value;

        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000), isNew: true);
        var value2 = pvt.Last.Value;

        Assert.Equal(0.0, value1);
        Assert.Equal(200.0, value2, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);  // First bar
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000), isNew: true);  // +10% -> +200

        var valueAfterSecond = pvt.Last.Value;  // Should be 200

        // Now correct the bar (isNew=false) with different values
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 105, 2000), isNew: false);  // +5% -> +100

        Assert.Equal(200.0, valueAfterSecond, Tolerance);
        Assert.Equal(100.0, pvt.Last.Value, Tolerance);  // Corrected to +5%
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreProperly()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);

        // Process a bar as new
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000), isNew: true);
        var originalValue = pvt.Last.Value;  // 200

        // Multiple corrections should all restore to same state
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 105, 2000), isNew: false);  // +5%
        Assert.Equal(100.0, pvt.Last.Value, Tolerance);

        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 102, 2000), isNew: false);  // +2%
        Assert.Equal(40.0, pvt.Last.Value, Tolerance);

        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000), isNew: false);  // Back to original +10%
        Assert.Equal(originalValue, pvt.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000));

        Assert.NotEqual(0.0, pvt.Last.Value);
        Assert.True(pvt.IsHot);

        pvt.Reset();

        Assert.Equal(0.0, pvt.Last.Value);
        Assert.False(pvt.IsHot);
    }

    // ==================== Warmup and IsHot Tests ====================

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmupPeriod()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        Assert.False(pvt.IsHot);

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));
        Assert.False(pvt.IsHot);

        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000));
        Assert.True(pvt.IsHot);
    }

    // ==================== NaN/Infinity Handling Tests ====================

    [Fact]
    public void Update_NaNClose_UsesLastValidClose()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000));  // PVT = 200
        var valueBeforeNaN = pvt.Last.Value;

        pvt.Update(new TBar(time.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        // Should use last valid close (110) for both prev and current -> 0% change
        Assert.Equal(valueBeforeNaN, pvt.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NaNVolume_UsesLastValidVolume()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));
        pvt.Update(new TBar(time.AddMinutes(1), 110, 115, 105, 110, 2000));  // PVT = 200
        pvt.Update(new TBar(time.AddMinutes(2), 110, 130, 110, 120, double.NaN));  // +9.09% with last valid vol

        // Uses last valid volume (2000) * (10/110) = ~181.82 added to 200
        Assert.True(pvt.Last.Value > 350 && pvt.Last.Value < 400);
    }

    [Fact]
    public void Update_InfinityClose_UsesLastValidClose()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000));
        var valueBeforeInf = pvt.Last.Value;

        pvt.Update(new TBar(time.AddMinutes(2), 110, double.PositiveInfinity, 110, double.PositiveInfinity, 1000));

        // Should use last valid close
        Assert.Equal(valueBeforeInf, pvt.Last.Value, Tolerance);
    }

    // ==================== Consistency Tests ====================

    [Fact]
    public void BatchCalculate_MatchesStreamingUpdate()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var pvtStreaming = new Pvt();
        var streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = pvtStreaming.Update(bars[i]).Value;
        }

        var batchResult = Pvt.Calculate(bars);

        // Compare last 45 values (after warmup)
        for (int i = 5; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult[i].Value, Tolerance);
        }
    }

    [Fact]
    public void SpanCalculate_MatchesStreamingUpdate()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var pvtStreaming = new Pvt();
        var streamingResults = new double[bars.Count];
        var close = new double[bars.Count];
        var volume = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = pvtStreaming.Update(bars[i]).Value;
            close[i] = bars[i].Close;
            volume[i] = bars[i].Volume;
        }

        var spanResult = new double[bars.Count];
        Pvt.Calculate(close, volume, spanResult);

        // Compare values after first bar
        for (int i = 1; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], spanResult[i], Tolerance);
        }
    }

    [Fact]
    public void EventPublishing_WorksCorrectly()
    {
        var pvt = new Pvt();
        var receivedValues = new List<TValue>();
        var receivedIsNew = new List<bool>();

        pvt.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValues.Add(args.Value);
            receivedIsNew.Add(args.IsNew);
        };

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 110, 100, 110, 2000);

        pvt.Update(bar1, isNew: true);
        pvt.Update(bar2, isNew: true);
        pvt.Update(bar2 with { Close = 105 }, isNew: false);

        Assert.Equal(3, receivedValues.Count);
        Assert.True(receivedIsNew[0]);
        Assert.True(receivedIsNew[1]);
        Assert.False(receivedIsNew[2]);
    }

    // ==================== Span API Validation Tests ====================

    [Fact]
    public void SpanCalculate_MismatchedLengths_Throws()
    {
        var close = new double[10];
        var volume = new double[8];  // Different length
        var output = new double[10];

        Assert.Throws<ArgumentException>(() => Pvt.Calculate(close, volume, output));
    }

    [Fact]
    public void SpanCalculate_OutputLengthMismatch_Throws()
    {
        var close = new double[10];
        var volume = new double[10];
        var output = new double[8];  // Wrong length

        Assert.Throws<ArgumentException>(() => Pvt.Calculate(close, volume, output));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_Succeeds()
    {
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        Pvt.Calculate(close, volume, output);  // Should not throw
        Assert.Empty(output);
    }

    [Fact]
    public void SpanCalculate_SingleElement_ReturnsZero()
    {
        var close = new double[] { 100.0 };
        var volume = new double[] { 1000.0 };
        var output = new double[1];

        Pvt.Calculate(close, volume, output);

        Assert.Equal(0.0, output[0]);
    }

    // ==================== Update with Price/Volume Direct ====================

    [Fact]
    public void Update_WithPriceVolume_WorksCorrectly()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow.Ticks;

        pvt.Update(100, 1000, time, isNew: true);  // First bar
        var result = pvt.Update(110, 2000, time + TimeSpan.TicksPerMinute, isNew: true);  // +10%

        Assert.Equal(200.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_TValueWithoutVolume_ReturnsUnchanged()
    {
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));
        pvt.Update(new TBar(time.AddMinutes(1), 100, 110, 100, 110, 2000));
        var pvtValue = pvt.Last.Value;

        // Update with TValue (no volume)
        var result = pvt.Update(new TValue(time.AddMinutes(2), 120));

        // Should remain unchanged since no volume
        Assert.Equal(pvtValue, result.Value);
    }

    [Fact]
    public void LargeDataset_HandlesWithoutError()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 10000; i++)
        {
            bars.Add(gbm.Next());
        }

        var pvt = new Pvt();
        foreach (var bar in bars)
        {
            var result = pvt.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(pvt.IsHot);
    }

    [Fact]
    public void FormulaVerification_ManualCalculation()
    {
        // Manual verification of PVT formula with known values
        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        // Bar 1: baseline (close = 100, volume = 10000)
        pvt.Update(new TBar(time, 100, 105, 95, 100, 10000));
        Assert.Equal(0, pvt.Last.Value); // First bar, PVT starts at 0

        // Bar 2: price up 10% (110 vs 100)
        // Expected: PVT = 0 + 15000 * (10/100) = 1500
        pvt.Update(new TBar(time.AddMinutes(1), 100, 115, 95, 110, 15000));
        Assert.Equal(1500, pvt.Last.Value, Tolerance);

        // Bar 3: price down (105 vs 110 = -4.545%)
        // Expected: PVT = 1500 + 12000 * (-5/110) = 1500 - 545.45... = 954.545...
        pvt.Update(new TBar(time.AddMinutes(2), 110, 112, 103, 105, 12000));
        Assert.True(pvt.Last.Value > 950 && pvt.Last.Value < 960);

        // Bar 4: price unchanged (105 == 105)
        // Expected: PVT unchanged
        var prevPvt = pvt.Last.Value;
        pvt.Update(new TBar(time.AddMinutes(3), 105, 108, 102, 105, 20000));
        Assert.Equal(prevPvt, pvt.Last.Value, Tolerance);
    }
}