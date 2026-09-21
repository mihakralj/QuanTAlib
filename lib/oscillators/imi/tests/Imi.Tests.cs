using System;
using Xunit;

namespace QuanTAlib.Tests;

public class ImiTests
{
    private const double Precision = 1e-10;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultPeriod_Is14()
    {
        var imi = new Imi();
        Assert.Equal(14, imi.Period);
    }

    [Fact]
    public void Constructor_CustomPeriod_IsSet()
    {
        var imi = new Imi(20);
        Assert.Equal(20, imi.Period);
    }

    [Fact]
    public void Constructor_Period1_IsValid()
    {
        var imi = new Imi(1);
        Assert.Equal(1, imi.Period);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Imi(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Imi(-1));
    }

    [Fact]
    public void Name_ReflectsPeriod()
    {
        var imi = new Imi(10);
        Assert.Equal("IMI(10)", imi.Name);
    }

    [Fact]
    public void WarmupPeriod_EqualsToPeriod()
    {
        var imi = new Imi(14);
        Assert.Equal(14, imi.WarmupPeriod);
    }

    #endregion

    #region IsHot Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var imi = new Imi(5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 4; i++)
        {
            imi.Update(new TBar(baseTime + (i * 60000), 100, 105, 95, 102, 1000));
        }

        Assert.False(imi.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var imi = new Imi(5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 5; i++)
        {
            imi.Update(new TBar(baseTime + (i * 60000), 100, 105, 95, 102, 1000));
        }

        Assert.True(imi.IsHot);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_AllUpBars_Returns100()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // All bars have Close > Open (bullish candlesticks)
        imi.Update(new TBar(baseTime, 100, 110, 99, 108, 1000));      // +8
        imi.Update(new TBar(baseTime + 60000, 105, 112, 104, 111, 1000)); // +6
        imi.Update(new TBar(baseTime + 120000, 108, 115, 107, 114, 1000)); // +6

        Assert.Equal(100.0, imi.Last.Value, Precision);
    }

    [Fact]
    public void Update_AllDownBars_Returns0()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // All bars have Close < Open (bearish candlesticks)
        imi.Update(new TBar(baseTime, 108, 110, 99, 100, 1000));      // -8
        imi.Update(new TBar(baseTime + 60000, 111, 112, 104, 105, 1000)); // -6
        imi.Update(new TBar(baseTime + 120000, 114, 115, 107, 108, 1000)); // -6

        Assert.Equal(0.0, imi.Last.Value, Precision);
    }

    [Fact]
    public void Update_MixedBars_CorrectCalculation()
    {
        var imi = new Imi(4);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Up bar: gain = 5, loss = 0
        imi.Update(new TBar(baseTime, 100, 110, 99, 105, 1000));

        // Down bar: gain = 0, loss = 3
        imi.Update(new TBar(baseTime + 60000, 105, 106, 100, 102, 1000));

        // Up bar: gain = 4, loss = 0
        imi.Update(new TBar(baseTime + 120000, 102, 108, 101, 106, 1000));

        // Down bar: gain = 0, loss = 2
        imi.Update(new TBar(baseTime + 180000, 106, 107, 103, 104, 1000));

        // Gains = 5 + 4 = 9, Losses = 3 + 2 = 5
        // IMI = 100 * 9 / (9 + 5) = 100 * 9 / 14 = 64.285714...
        double expected = 100.0 * 9.0 / 14.0;
        Assert.Equal(expected, imi.Last.Value, Precision);
    }

    [Fact]
    public void Update_AllDoji_Returns50()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // All bars have Close == Open (doji candlesticks)
        imi.Update(new TBar(baseTime, 100, 105, 95, 100, 1000));
        imi.Update(new TBar(baseTime + 60000, 100, 108, 92, 100, 1000));
        imi.Update(new TBar(baseTime + 120000, 100, 103, 97, 100, 1000));

        // Sum of gains = 0, Sum of losses = 0, total = 0, returns 50 (neutral)
        Assert.Equal(50.0, imi.Last.Value, Precision);
    }

    [Fact]
    public void Update_EqualGainsAndLosses_Returns50()
    {
        var imi = new Imi(2);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Up bar: gain = 5
        imi.Update(new TBar(baseTime, 100, 110, 99, 105, 1000));

        // Down bar: loss = 5
        imi.Update(new TBar(baseTime + 60000, 105, 106, 99, 100, 1000));

        // Gains = 5, Losses = 5, IMI = 50
        Assert.Equal(50.0, imi.Last.Value, Precision);
    }

    #endregion

    #region Rolling Window Tests

    [Fact]
    public void Update_RollingWindow_DropsOldValues()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Fill with up bars
        imi.Update(new TBar(baseTime, 100, 110, 99, 110, 1000));       // +10
        imi.Update(new TBar(baseTime + 60000, 100, 110, 99, 110, 1000)); // +10
        imi.Update(new TBar(baseTime + 120000, 100, 110, 99, 110, 1000)); // +10
        Assert.Equal(100.0, imi.Last.Value, Precision);

        // Add a down bar - oldest up bar should drop off
        imi.Update(new TBar(baseTime + 180000, 110, 111, 99, 100, 1000)); // -10

        // Now: gains = 10 + 10 = 20, losses = 10
        // IMI = 100 * 20 / 30 = 66.666...
        double expected = 100.0 * 20.0 / 30.0;
        Assert.Equal(expected, imi.Last.Value, Precision);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_BarCorrection_RestoresPreviousState()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Fill initial data
        imi.Update(new TBar(baseTime, 100, 105, 95, 103, 1000));
        imi.Update(new TBar(baseTime + 60000, 100, 105, 95, 104, 1000));
        imi.Update(new TBar(baseTime + 120000, 100, 105, 95, 105, 1000));

        // Add new bar (up)
        imi.Update(new TBar(baseTime + 180000, 100, 107, 99, 106, 1000), isNew: true);
        double valueAfterNew = imi.Last.Value;

        // Correct the bar (now down)
        imi.Update(new TBar(baseTime + 180000, 106, 107, 93, 94, 1000), isNew: false);
        double valueAfterCorrection = imi.Last.Value;

        // Values should differ based on the correction
        Assert.NotEqual(valueAfterNew, valueAfterCorrection);
    }

    [Fact]
    public void Update_MultipleCorrections_ProduceConsistentResults()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Fill buffer
        for (int i = 0; i < 3; i++)
        {
            imi.Update(new TBar(baseTime + (i * 60000), 100, 105, 95, 102, 1000));
        }

        // New bar
        imi.Update(new TBar(baseTime + (3 * 60000), 100, 110, 99, 108, 1000), isNew: true);
        double firstValue = imi.Last.Value;

        // Correction 1
        imi.Update(new TBar(baseTime + (3 * 60000), 100, 115, 99, 92, 1000), isNew: false);

        // Correction 2 - same as first new bar
        imi.Update(new TBar(baseTime + (3 * 60000), 100, 110, 99, 108, 1000), isNew: false);
        double secondValue = imi.Last.Value;

        Assert.Equal(firstValue, secondValue, Precision);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaNOpen_KeepsPreviousValue()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        imi.Update(new TBar(baseTime, 100, 105, 95, 103, 1000));
        double validValue = imi.Last.Value;

        imi.Update(new TBar(baseTime + 60000, double.NaN, 110, 99, 108, 1000));

        Assert.Equal(validValue, imi.Last.Value);
    }

    [Fact]
    public void Update_NaNClose_KeepsPreviousValue()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        imi.Update(new TBar(baseTime, 100, 105, 95, 103, 1000));
        double validValue = imi.Last.Value;

        imi.Update(new TBar(baseTime + 60000, 105, 110, 99, double.NaN, 1000));

        Assert.Equal(validValue, imi.Last.Value);
    }

    [Fact]
    public void Update_InfinityValues_KeepsPreviousValue()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        imi.Update(new TBar(baseTime, 100, 105, 95, 103, 1000));
        double validValue = imi.Last.Value;

        imi.Update(new TBar(baseTime + 60000, double.PositiveInfinity, 110, 99, 108, 1000));

        Assert.Equal(validValue, imi.Last.Value);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 5; i++)
        {
            imi.Update(new TBar(baseTime + (i * 60000), 100, 110, 99, 108, 1000));
        }

        Assert.True(imi.IsHot);

        imi.Reset();

        Assert.False(imi.IsHot);
        Assert.Equal(0, imi.Last.Value);
    }

    [Fact]
    public void Reset_AllowsFreshStart()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // All up bars
        for (int i = 0; i < 3; i++)
        {
            imi.Update(new TBar(baseTime + (i * 60000), 100, 110, 99, 108, 1000));
        }
        Assert.Equal(100.0, imi.Last.Value, Precision);

        imi.Reset();

        // All down bars
        for (int i = 0; i < 3; i++)
        {
            imi.Update(new TBar(baseTime + (i * 60000), 108, 110, 99, 100, 1000));
        }
        Assert.Equal(0.0, imi.Last.Value, Precision);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_FillsBuffer()
    {
        var imi = new Imi(5);
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TBar(baseTime + (i * 60000), 100, 110, 99, 108, 1000));
        }

        imi.Prime(source);

        Assert.True(imi.IsHot);
        Assert.Equal(100.0, imi.Last.Value, Precision);
    }

    #endregion

    #region Batch Tests

    [Fact]
    public void Batch_ReturnsSeriesOfCorrectLength()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TBar(baseTime + (i * 60000), 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        var result = Imi.Batch(source);

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void Batch_EmptySource_ReturnsEmpty()
    {
        var source = new TBarSeries();
        var result = Imi.Batch(source);

        Assert.Empty(result);
    }

    [Fact]
    public void Batch_CustomPeriod_AppliesCorrectly()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TBar(baseTime + (i * 60000), 100, 110, 99, 108, 1000));
        }

        var result = Imi.Batch(source, 5);

        Assert.Equal(20, result.Count);
        Assert.Equal(100.0, result[^1].Value, Precision);
    }

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var source = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TBar(baseTime + (i * 60000), 100, 110, 99, 108, 1000));
        }

        var (results, indicator) = Imi.Calculate(source, 10);

        Assert.Equal(20, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(10, indicator.Period);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void Update_PublishesEvent()
    {
        var imi = new Imi(3);
        int eventCount = 0;
        imi.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        imi.Update(new TBar(baseTime, 100, 110, 99, 105, 1000));

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Update_EventContainsCorrectValue()
    {
        var imi = new Imi(3);
        TValue? receivedValue = null;
        imi.Pub += (object? sender, in TValueEventArgs args) => receivedValue = args.Value;

        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        imi.Update(new TBar(baseTime, 100, 110, 99, 110, 1000));

        Assert.NotNull(receivedValue);
        Assert.Equal(imi.Last.Value, receivedValue.Value.Value);
    }

    #endregion

    #region GBM Random Data Test

    [Fact]
    public void Update_GbmData_ReturnsValueInRange()
    {
        var imi = new Imi(14);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            imi.Update(bars[i]);

            // IMI should always be in [0, 100]
            Assert.InRange(imi.Last.Value, 0.0, 100.0);
        }
    }

    #endregion
}
