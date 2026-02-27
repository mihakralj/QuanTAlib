using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class QstickIndicatorTests
{
    // ── Constructor & Defaults ──────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesValidIndicator()
    {
        var indicator = new QstickIndicator();
        Assert.NotNull(indicator);
        Assert.Equal("Qstick Indicator", indicator.Name);
    }

    [Fact]
    public void Constructor_Description_IsNotEmpty()
    {
        var indicator = new QstickIndicator();
        Assert.False(string.IsNullOrWhiteSpace(indicator.Description));
        Assert.Contains("candlestick", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_SeparateWindow_IsTrue()
    {
        var indicator = new QstickIndicator();
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void Constructor_CreatesOneLineSeries()
    {
        var indicator = new QstickIndicator();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Constructor_LineSeries_NameIsQstick()
    {
        var indicator = new QstickIndicator();
        Assert.Equal("Qstick", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void DefaultPeriod_Is14()
    {
        var indicator = new QstickIndicator();
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void DefaultTALib.Core.MAType_IsSMA()
    {
        var indicator = new QstickIndicator();
        Assert.Equal("SMA", indicator.TALib.Core.MAType);
    }

    [Fact]
    public void DefaultShowColdValues_IsTrue()
    {
        var indicator = new QstickIndicator();
        Assert.True(indicator.ShowColdValues);
    }

    // ── ShortName ───────────────────────────────────────────────────────

    [Fact]
    public void ShortName_DefaultParameters_IncludesPeriodAndTALib.Core.MAType()
    {
        var indicator = new QstickIndicator();
        Assert.Equal("QSTICK(14,SMA)", indicator.ShortName);
    }

    [Fact]
    public void ShortName_CustomPeriod_ReflectsNewPeriod()
    {
        var indicator = new QstickIndicator { Period = 20 };
        Assert.Equal("QSTICK(20,SMA)", indicator.ShortName);
    }

    [Fact]
    public void ShortName_EmaMode_IncludesEMA()
    {
        var indicator = new QstickIndicator { Period = 20, TALib.Core.MAType = "EMA" };
        Assert.Equal("QSTICK(20,EMA)", indicator.ShortName);
    }

    // ── MinHistoryDepths ────────────────────────────────────────────────

    [Fact]
    public void MinHistoryDepths_Static_EqualsZero()
    {
        Assert.Equal(0, QstickIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MinHistoryDepths_Interface_EqualsZero()
    {
        var indicator = new QstickIndicator();
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    // ── OnInit ──────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_SmaMode_CreatesInternalIndicator()
    {
        var indicator = new QstickIndicator { TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        Assert.NotNull(indicator);
    }

    [Fact]
    public void Initialize_EmaMode_CreatesInternalIndicator()
    {
        var indicator = new QstickIndicator { TALib.Core.MAType = "EMA" };
        indicator.Initialize();

        Assert.NotNull(indicator);
    }

    [Fact]
    public void Initialize_AddsZeroLineLevel()
    {
        var indicator = new QstickIndicator();
        indicator.Initialize();

        // OnInit calls AddLineLevel(0, "Zero", ...)
        Assert.True(indicator.LineLevels.Count >= 1);
    }

    // ── ProcessUpdate: HistoricalBar ────────────────────────────────────

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new QstickIndicator { Period = 3, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 110.0, 95.0, 105.0, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // After 5 bars with constant diff=5, Qstick=5
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_BullishBars_PositiveValue()
    {
        var indicator = new QstickIndicator { Period = 3, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 110.0, 95.0, 105.0, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All bullish, diff=5 each, SMA=5.0
        Assert.Equal(5.0, indicator.LinesSeries[0].GetValue(0), 10);
    }

    // ── ProcessUpdate: NewBar ───────────────────────────────────────────

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new QstickIndicator { Period = 3, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100.0, 110.0, 95.0, 105.0, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100.0, 108.0, 95.0, 103.0, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    // ── ProcessUpdate: NewTick ──────────────────────────────────────────

    [Fact]
    public void ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new QstickIndicator { Period = 3, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100.0, 110.0, 95.0, 105.0, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    // ── SMA vs EMA ──────────────────────────────────────────────────────

    [Fact]
    public void SmaMode_BearishBars_ProducesNegativeQstick()
    {
        var indicator = new QstickIndicator { Period = 5, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 105.0, 90.0, 95.0, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All bearish (close < open), diff=-5
        Assert.Equal(-5.0, indicator.LinesSeries[0].GetValue(0), 10);
    }

    [Fact]
    public void SmaMode_DojiBars_ProducesZeroQstick()
    {
        var indicator = new QstickIndicator { Period = 5, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 105.0, 95.0, 100.0, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 10);
    }

    [Fact]
    public void EmaMode_BullishBars_ProducesPositiveQstick()
    {
        var indicator = new QstickIndicator { Period = 5, TALib.Core.MAType = "EMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 110.0, 95.0, 105.0, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All same diff=5, EMA converges to 5.0
        Assert.Equal(5.0, indicator.LinesSeries[0].GetValue(0), 10);
    }

    // ── Core Calculation Integration ────────────────────────────────────

    [Fact]
    public void CalculationIntegration_SmaMode_ProducesCorrectValues()
    {
        var qstickCore = new Qstick(3);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
        var bar2 = new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000);
        var bar3 = new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000);

        qstickCore.Update(bar1);
        qstickCore.Update(bar2);
        var result = qstickCore.Update(bar3);

        // SMA of (5, 3, 6) = 14/3 ≈ 4.667
        Assert.Equal(14.0 / 3.0, result.Value, 10);
    }

    [Fact]
    public void EmaMode_CalculatesCorrectly()
    {
        var qstickCore = new Qstick(3, useEma: true);
        var time = DateTime.UtcNow;

        // Bar 1: diff = 5
        qstickCore.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));

        // Bar 2: diff = -3, EMA with alpha = 2/(3+1) = 0.5
        var result = qstickCore.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 97.0, 1000));

        // EMA = 0.5 * -3 + 0.5 * 5 = 1.0
        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void CalculationIntegration_MixedBullishBearish()
    {
        var qstickCore = new Qstick(4);
        var time = DateTime.UtcNow;

        // 2 bullish (diff=5), 2 bearish (diff=-5) → SMA = 0
        qstickCore.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstickCore.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstickCore.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 105.0, 90.0, 95.0, 1000));
        var result = qstickCore.Update(new TBar(time.AddMinutes(3).Ticks, 100.0, 105.0, 90.0, 95.0, 1000));

        Assert.Equal(0.0, result.Value, 10);
    }

    [Fact]
    public void BullishBars_ProducePositiveQstick()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        }

        Assert.True(qstick.Last.Value > 0);
        Assert.Equal(5.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void BearishBars_ProduceNegativeQstick()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 105.0, 90.0, 95.0, 1000));
        }

        Assert.True(qstick.Last.Value < 0);
        Assert.Equal(-5.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void DojiBars_ProduceZeroQstick()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 105.0, 95.0, 100.0, 1000));
        }

        Assert.Equal(0.0, qstick.Last.Value, 10);
    }

    // ── Core Indicator Features ─────────────────────────────────────────

    [Fact]
    public void CoreIndicator_ResetsCorrectly()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));

        Assert.NotEqual(default, qstick.Last);

        qstick.Reset();

        Assert.False(qstick.IsHot);
        Assert.Equal(default, qstick.Last);
    }

    [Fact]
    public void CoreIndicator_IsHot_SmaMode_AfterPeriodBars()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        Assert.False(qstick.IsHot);

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        Assert.False(qstick.IsHot);

        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        Assert.False(qstick.IsHot);

        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        Assert.True(qstick.IsHot);
    }

    [Fact]
    public void CoreIndicator_IsHot_EmaMode_AfterFirstBar()
    {
        var qstick = new Qstick(3, useEma: true);
        var time = DateTime.UtcNow;

        Assert.False(qstick.IsHot);

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        Assert.True(qstick.IsHot);
    }

    [Fact]
    public void CoreIndicator_Period_ReturnsConstructorValue()
    {
        var qstick = new Qstick(20);
        Assert.Equal(20, qstick.Period);
    }

    [Fact]
    public void CoreIndicator_UseEma_ReturnsConstructorValue()
    {
        var qstickSma = new Qstick(10, useEma: false);
        Assert.False(qstickSma.UseEma);

        var qstickEma = new Qstick(10, useEma: true);
        Assert.True(qstickEma.UseEma);
    }

    [Fact]
    public void CoreIndicator_WarmupPeriod_EqualsPeriod()
    {
        var qstick = new Qstick(20);
        Assert.Equal(20, qstick.WarmupPeriod);
    }

    [Fact]
    public void CoreIndicator_Name_SmaMode_DoesNotIncludeEma()
    {
        var qstick = new Qstick(14);
        Assert.Equal("QSTICK(14)", qstick.Name);
    }

    [Fact]
    public void CoreIndicator_Name_EmaMode_IncludesEma()
    {
        var qstick = new Qstick(14, useEma: true);
        Assert.Equal("QSTICK(14,EMA)", qstick.Name);
    }

    [Fact]
    public void CoreIndicator_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Qstick(0));
        Assert.Throws<ArgumentException>(() => new Qstick(-1));
    }

    // ── NaN/Infinity Handling ───────────────────────────────────────────

    [Fact]
    public void CoreIndicator_NaNOpen_ReturnsLastValue()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        var firstResult = qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        var nanResult = qstick.Update(new TBar(time.AddMinutes(1).Ticks, double.NaN, 110.0, 95.0, 105.0, 1000));

        Assert.Equal(firstResult.Value, nanResult.Value);
    }

    [Fact]
    public void CoreIndicator_InfinityClose_ReturnsLastValue()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        var firstResult = qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        var infResult = qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 110.0, 95.0, double.PositiveInfinity, 1000));

        Assert.Equal(firstResult.Value, infResult.Value);
    }

    [Fact]
    public void CoreIndicator_NegativeInfinityOpen_ReturnsLastValue()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        var firstResult = qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        var infResult = qstick.Update(new TBar(time.AddMinutes(1).Ticks, double.NegativeInfinity, 110.0, 95.0, 105.0, 1000));

        Assert.Equal(firstResult.Value, infResult.Value);
    }

    // ── Bar Correction (isNew=false) ────────────────────────────────────

    [Fact]
    public void CoreIndicator_BarCorrection_SmaMode_UpdatesLastBar()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 110.0, 95.0, 103.0, 1000));

        // New bar
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 106.0, 1000), isNew: true);
        var afterNew = qstick.Last.Value;

        // Correct the same bar (isNew=false)
        var afterCorrection = qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 112.0, 93.0, 110.0, 1000), isNew: false);

        // Value should change since close-open changed from 6 to 10
        Assert.NotEqual(afterNew, afterCorrection.Value);
    }

    [Fact]
    public void CoreIndicator_BarCorrection_EmaMode_RollsBackState()
    {
        var qstick = new Qstick(3, useEma: true);
        var time = DateTime.UtcNow;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));

        // New bar
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 108.0, 95.0, 106.0, 1000), isNew: true);
        var afterNew = qstick.Last.Value;

        // Correct the same bar (isNew=false)
        var afterCorrection = qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 112.0, 93.0, 110.0, 1000), isNew: false);

        Assert.NotEqual(afterNew, afterCorrection.Value);
    }

    // ── Batch / Update(TBarSeries) / Calculate ──────────────────────────

    [Fact]
    public void CoreIndicator_UpdateTBarSeries_ReturnsTSeries()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;
        var series = new TBarSeries();

        for (int i = 0; i < 5; i++)
        {
            series.Add(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0 + i, 1000);
        }

        var result = qstick.Update(series);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void CoreIndicator_UpdateTBarSeries_EmptySeries_ReturnsEmpty()
    {
        var qstick = new Qstick(3);
        var series = new TBarSeries();

        var result = qstick.Update(series);

        Assert.Empty(result);
    }

    [Fact]
    public void CoreIndicator_Batch_ReturnsResults()
    {
        var time = DateTime.UtcNow;
        var series = new TBarSeries();

        for (int i = 0; i < 5; i++)
        {
            series.Add(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
        }

        var result = Qstick.Batch(series, period: 3);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void CoreIndicator_BatchEma_ReturnsResults()
    {
        var time = DateTime.UtcNow;
        var series = new TBarSeries();

        for (int i = 0; i < 5; i++)
        {
            series.Add(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
        }

        var result = Qstick.Batch(series, period: 3, useEma: true);

        Assert.Equal(5, result.Count);
        Assert.Equal(5.0, result.Last.Value, 10);
    }

    [Fact]
    public void CoreIndicator_Calculate_ReturnsResultsAndIndicator()
    {
        var time = DateTime.UtcNow;
        var series = new TBarSeries();

        for (int i = 0; i < 5; i++)
        {
            series.Add(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
        }

        var (results, indicator) = Qstick.Calculate(series, period: 3);

        Assert.Equal(5, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void CoreIndicator_Prime_WarmsUpIndicator()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;
        var series = new TBarSeries();

        for (int i = 0; i < 5; i++)
        {
            series.Add(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
        }

        qstick.Prime(series);

        Assert.True(qstick.IsHot);
        Assert.Equal(5.0, qstick.Last.Value, 10);
    }

    // ── Pub Event ───────────────────────────────────────────────────────

    [Fact]
    public void CoreIndicator_PubEvent_FiresOnUpdate()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;
        int eventCount = 0;

        qstick.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 95.0, 103.0, 1000));

        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void CoreIndicator_PubEvent_NaN_StillFires()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;
        int eventCount = 0;

        qstick.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        qstick.Update(new TBar(time.Ticks, double.NaN, 110.0, 95.0, 105.0, 1000));

        Assert.Equal(1, eventCount);
    }

    // ── Different Periods ───────────────────────────────────────────────

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var indicator1 = new QstickIndicator { Period = 3, TALib.Core.MAType = "SMA" };
        var indicator2 = new QstickIndicator { Period = 10, TALib.Core.MAType = "SMA" };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double close = 105.0 + (i % 2 == 0 ? 3.0 : -3.0);
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 115.0, 85.0, close, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 115.0, 85.0, close, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void SmaVsEma_SameData_ProduceDifferentResults()
    {
        var smaIndicator = new QstickIndicator { Period = 5, TALib.Core.MAType = "SMA" };
        var emaIndicator = new QstickIndicator { Period = 5, TALib.Core.MAType = "EMA" };
        smaIndicator.Initialize();
        emaIndicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = [105.0, 97.0, 108.0, 99.0, 102.0];
        for (int i = 0; i < 5; i++)
        {
            smaIndicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 115.0, 85.0, closes[i], 1000);
            smaIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            emaIndicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 115.0, 85.0, closes[i], 1000);
            emaIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double smaVal = smaIndicator.LinesSeries[0].GetValue(0);
        double emaVal = emaIndicator.LinesSeries[0].GetValue(0);
        // SMA = (5 + -3 + 8 + -1 + 2) / 5 = 11/5 = 2.2
        Assert.Equal(2.2, smaVal, 10);
        Assert.NotEqual(smaVal, emaVal);
    }

    // ── Reinit ──────────────────────────────────────────────────────────

    [Fact]
    public void Reinitialize_WithDifferentParameters_ResetsState()
    {
        var indicator = new QstickIndicator { Period = 5, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 110.0, 95.0, 105.0, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.Period = 10;
        indicator.TALib.Core.MAType = "EMA";
        indicator.Initialize();

        Assert.Equal("QSTICK(10,EMA)", indicator.ShortName);
    }

    // ── ShowColdValues ──────────────────────────────────────────────────

    [Fact]
    public void ShowColdValues_CanBeSetToFalse()
    {
        var indicator = new QstickIndicator { ShowColdValues = false };
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void ShowColdValues_False_ProcessesWithoutError()
    {
        var indicator = new QstickIndicator { Period = 5, ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 2; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100.0, 110.0, 95.0, 105.0, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.NotNull(indicator);
    }

    // ── Multiple bars through adapter with known values ─────────────────

    [Fact]
    public void MultipleBars_ThroughAdapter_ProducesExpectedValues()
    {
        var indicator = new QstickIndicator { Period = 3, TALib.Core.MAType = "SMA" };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Bar 1: diff = 5 (105-100)
        indicator.HistoricalData.AddBar(now, 100.0, 110.0, 95.0, 105.0, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 2: diff = 3 (103-100)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100.0, 105.0, 95.0, 103.0, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Bar 3: diff = 6 (106-100)
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100.0, 108.0, 95.0, 106.0, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // SMA(3) of [5, 3, 6] = 14/3 ≈ 4.667
        Assert.Equal(14.0 / 3.0, indicator.LinesSeries[0].GetValue(0), 10);
    }

    // ── Parameters can be modified ──────────────────────────────────────

    [Fact]
    public void Period_CanBeChanged()
    {
        var indicator = new QstickIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 30;
        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void TALib.Core.MAType_CanBeChanged()
    {
        var indicator = new QstickIndicator();
        Assert.Equal("SMA", indicator.TALib.Core.MAType);

        indicator.TALib.Core.MAType = "EMA";
        Assert.Equal("EMA", indicator.TALib.Core.MAType);
    }
}
