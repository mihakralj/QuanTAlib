using Xunit;

namespace QuanTAlib.Tests;

public class QstickTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Constructor Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var qstick = new Qstick();
        Assert.Equal(14, qstick.Period);
        Assert.False(qstick.UseEma);
        Assert.Equal("QSTICK(14)", qstick.Name);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var qstick = new Qstick(20);
        Assert.Equal(20, qstick.Period);
    }

    [Fact]
    public void Constructor_EmaMode_SetsCorrectly()
    {
        var qstick = new Qstick(14, useEma: true);
        Assert.True(qstick.UseEma);
        Assert.Equal("QSTICK(14,EMA)", qstick.Name);
    }

    [Fact]
    public void Constructor_PeriodLessThanOne_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Qstick(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Qstick(-1));
    }

    [Fact]
    public void Constructor_PeriodOne_IsValid()
    {
        var qstick = new Qstick(1);
        Assert.Equal(1, qstick.Period);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Basic Calculation Tests - SMA Mode
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_SingleBullishBar_ReturnsPositiveValue()
    {
        var qstick = new Qstick(1);
        var bar = new TBar(DateTime.UtcNow.Ticks, 100.0, 105.0, 99.0, 105.0, 1000);
        var result = qstick.Update(bar);
        Assert.Equal(5.0, result.Value);  // close - open = 105 - 100 = 5
    }

    [Fact]
    public void Update_SingleBearishBar_ReturnsNegativeValue()
    {
        var qstick = new Qstick(1);
        var bar = new TBar(DateTime.UtcNow.Ticks, 105.0, 105.0, 99.0, 100.0, 1000);
        var result = qstick.Update(bar);
        Assert.Equal(-5.0, result.Value);  // close - open = 100 - 105 = -5
    }

    [Fact]
    public void Update_DojiBar_ReturnsZero()
    {
        var qstick = new Qstick(1);
        var bar = new TBar(DateTime.UtcNow.Ticks, 100.0, 105.0, 99.0, 100.0, 1000);
        var result = qstick.Update(bar);
        Assert.Equal(0.0, result.Value);  // close - open = 100 - 100 = 0
    }

    [Fact]
    public void Update_ThreeBars_CalculatesCorrectSMA()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        // Bar 1: +5 (bullish)
        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));

        // Bar 2: -3 (bearish)
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 97.0, 1000));

        // Bar 3: +2 (bullish)
        var result = qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));

        // SMA = (5 + -3 + 2) / 3 = 4/3 ≈ 1.333
        Assert.Equal(4.0 / 3.0, result.Value, 10);
        Assert.True(qstick.IsHot);
    }

    [Fact]
    public void Update_NotWarmUp_ReturnsPartialAverage()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        // Bar 1: +5
        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));

        // Bar 2: +3
        var result = qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));

        // Partial average = (5 + 3) / 2 = 4
        Assert.Equal(4.0, result.Value, 10);
        Assert.False(qstick.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Basic Calculation Tests - EMA Mode
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_EmaMode_FirstBar_ReturnsDiff()
    {
        var qstick = new Qstick(14, useEma: true);
        var bar = new TBar(DateTime.UtcNow.Ticks, 100.0, 105.0, 99.0, 105.0, 1000);
        var result = qstick.Update(bar);
        Assert.Equal(5.0, result.Value);
    }

    [Fact]
    public void Update_EmaMode_MultipleBar_CalculatesEma()
    {
        var qstick = new Qstick(3, useEma: true);  // alpha = 2/(3+1) = 0.5
        var time = DateTime.UtcNow;

        // Bar 1: diff = 5
        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));

        // Bar 2: diff = -3, EMA = 0.5 * -3 + 0.5 * 5 = 1.0
        var result = qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 97.0, 1000));

        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void Update_EmaMode_IsHotFromFirstBar()
    {
        var qstick = new Qstick(14, useEma: true);
        var bar = new TBar(DateTime.UtcNow.Ticks, 100.0, 105.0, 99.0, 105.0, 1000);
        qstick.Update(bar);
        Assert.True(qstick.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Bar Correction Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_IsNewFalse_CorrectsPreviousBar()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        // Bar 1
        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));

        // Bar 2
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));

        // Bar 3 initial
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));

        // Bar 3 correction (close changes from 102 to 108)
        var result = qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 108.0, 1000), isNew: false);

        // SMA = (5 + 3 + 8) / 3 = 16/3 ≈ 5.333
        Assert.Equal(16.0 / 3.0, result.Value, 10);
    }

    [Fact]
    public void Update_MultipleCorrections_ProducesSameResult()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));

        // Multiple corrections to same bar should be idempotent
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 107.0, 1000), isNew: false);
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 107.0, 1000), isNew: false);
        var result = qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 107.0, 1000), isNew: false);

        // SMA = (5 + 3 + 7) / 3 = 15/3 = 5
        Assert.Equal(5.0, result.Value, 10);
    }

    [Fact]
    public void Update_EmaMode_BarCorrection_Works()
    {
        var qstick = new Qstick(3, useEma: true);  // alpha = 0.5
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));  // diff=5, ema=5
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));  // diff=3, ema=0.5*3+0.5*5=4
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));  // diff=2, ema=0.5*2+0.5*4=3

        // Correct bar 3: diff changes from 2 to 8
        var result = qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 108.0, 1000), isNew: false);

        // ema = 0.5*8 + 0.5*4 = 6
        Assert.Equal(6.0, result.Value, 10);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Edge Case Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_NaNOpen_ReturnsLastValue()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time.Ticks, 100.0, 105.0, 99.0, 105.0, 1000);
        var result1 = qstick.Update(bar1);

        var bar2 = new TBar(time.AddMinutes(1).Ticks, double.NaN, 105.0, 99.0, 105.0, 1000);
        var result2 = qstick.Update(bar2);

        Assert.Equal(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_NaNClose_ReturnsLastValue()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time.Ticks, 100.0, 105.0, 99.0, 105.0, 1000);
        var result1 = qstick.Update(bar1);

        var bar2 = new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 99.0, double.NaN, 1000);
        var result2 = qstick.Update(bar2);

        Assert.Equal(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_InfinityInput_ReturnsLastValue()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time.Ticks, 100.0, 105.0, 99.0, 105.0, 1000);
        var result1 = qstick.Update(bar1);

        var bar2 = new TBar(time.AddMinutes(1).Ticks, double.PositiveInfinity, 105.0, 99.0, 105.0, 1000);
        var result2 = qstick.Update(bar2);

        Assert.Equal(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_LargeValues_CalculatesCorrectly()
    {
        var qstick = new Qstick(1);
        var bar = new TBar(DateTime.UtcNow.Ticks, 1e10, 1.1e10, 0.9e10, 1.05e10, 1000);
        var result = qstick.Update(bar);
        Assert.Equal(0.05e10, result.Value, 1);
    }

    [Fact]
    public void Update_SmallValues_CalculatesCorrectly()
    {
        var qstick = new Qstick(1);
        var bar = new TBar(DateTime.UtcNow.Ticks, 0.0001, 0.00015, 0.00009, 0.00012, 1000);
        var result = qstick.Update(bar);
        Assert.Equal(0.00002, result.Value, 10);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reset Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_ClearsState()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));

        Assert.True(qstick.IsHot);

        qstick.Reset();

        Assert.False(qstick.IsHot);
        Assert.Equal(default, qstick.Last);
    }

    [Fact]
    public void Reset_EmaMode_ClearsState()
    {
        var qstick = new Qstick(3, useEma: true);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));

        Assert.True(qstick.IsHot);

        qstick.Reset();

        Assert.False(qstick.IsHot);
    }

    [Fact]
    public void Reset_CanReuseAfterReset()
    {
        var qstick = new Qstick(2);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));

        qstick.Reset();

        // New data after reset
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 105.0, 95.0, 98.0, 1000));  // diff = -2
        var result = qstick.Update(new TBar(time.AddMinutes(3).Ticks, 100.0, 105.0, 95.0, 106.0, 1000));  // diff = 6

        // SMA = (-2 + 6) / 2 = 2
        Assert.Equal(2.0, result.Value, 10);
        Assert.True(qstick.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Batch Processing Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_BarSeries_ReturnsCorrectLength()
    {
        var qstick = new Qstick(3);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        bars.Add(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));
        bars.Add(new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000));

        var result = qstick.Update(bars);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Update_BarSeries_LastValueMatchesLast()
    {
        var qstick = new Qstick(3);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        bars.Add(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));
        bars.Add(new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000));

        var result = qstick.Update(bars);

        Assert.Equal(qstick.Last.Value, result.Values[^1], 10);
    }

    [Fact]
    public void Update_EmptyBarSeries_ReturnsEmpty()
    {
        var qstick = new Qstick(3);
        var bars = new TBarSeries();

        var result = qstick.Update(bars);

        Assert.True(result.Count == 0);
    }

    [Fact]
    public void Batch_ReturnsCorrectResults()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));  // diff = 5
        bars.Add(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));  // diff = 3
        bars.Add(new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000));  // diff = 6

        var result = Qstick.Batch(bars, period: 3);

        // Last value = SMA(5, 3, 6) = 14/3 ≈ 4.667
        Assert.Equal(14.0 / 3.0, result.Values[^1], 10);
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        bars.Add(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));
        bars.Add(new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000));

        var (results, indicator) = Qstick.Calculate(bars, period: 3);

        Assert.Equal(3, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(3, indicator.Period);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Prime Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Prime_WarmUpIndicator()
    {
        var qstick = new Qstick(3);
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        bars.Add(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));
        bars.Add(new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000));

        qstick.Prime(bars);

        Assert.True(qstick.IsHot);
        Assert.NotEqual(default, qstick.Last);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Event Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_RaisesPubEvent()
    {
        var qstick = new Qstick(3);
        var eventRaised = false;
        TValue receivedValue = default;

        qstick.Pub += (object? sender, in TValueEventArgs args) =>
        {
            eventRaised = true;
            receivedValue = args.Value;
        };

        var bar = new TBar(DateTime.UtcNow.Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
        var result = qstick.Update(bar);

        Assert.True(eventRaised);
        Assert.Equal(result.Value, receivedValue.Value);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Property Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var qstick = new Qstick(20);
        Assert.Equal(20, qstick.WarmupPeriod);
    }

    [Fact]
    public void IsHot_SmaMode_FalseBeforeFullPeriod()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));
            Assert.False(qstick.IsHot);
        }

        qstick.Update(new TBar(time.AddMinutes(4).Ticks, 100.0, 105.0, 95.0, 102.0, 1000));
        Assert.True(qstick.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Consistency Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_BatchVsStreaming_ProducesSameResults()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double open = 100.0 + i * 0.5;
            double close = open + (i % 3 - 1);  // varies between -1, 0, 1
            bars.Add(new TBar(time.AddMinutes(i).Ticks, open, open + 2, open - 1, close, 1000));
        }

        // Batch processing
        var batchQstick = new Qstick(5);
        var batchResult = batchQstick.Update(bars);

        // Streaming processing
        var streamQstick = new Qstick(5);
        var streamResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamQstick.Update(bars[i]);
            streamResults.Add(result.Value);
        }

        // Compare results
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamResults[i], 10);
        }
    }

    [Fact]
    public void SmaVsEma_DifferentResults()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double open = 100.0;
            double close = 100.0 + (i % 2 == 0 ? 5 : -3);
            bars.Add(new TBar(time.AddMinutes(i).Ticks, open, 110.0, 95.0, close, 1000));
        }

        var smaQstick = new Qstick(5, useEma: false);
        var emaQstick = new Qstick(5, useEma: true);

        var smaResult = smaQstick.Update(bars);
        var emaResult = emaQstick.Update(bars);

        // SMA and EMA should produce different results (EMA weights more recent)
        Assert.NotEqual(smaResult.Values[^1], emaResult.Values[^1]);
    }
}
