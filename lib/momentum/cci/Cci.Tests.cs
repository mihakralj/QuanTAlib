using Xunit;

namespace QuanTAlib.Tests;

public class CciTests
{
    private readonly TBarSeries _bars;

    public CciTests()
    {
        _bars = new TBarSeries(capacity: 100);
        var baseTime = DateTime.UtcNow.Date;
        // Create test bars with known patterns
        for (int i = 0; i < 50; i++)
        {
            double high = 100 + i + 2;
            double low = 100 + i - 2;
            double open = 100 + i - 1;
            double close = 100 + i + 1;
            _bars.Add(new TBar(baseTime.AddDays(i), open, high, low, close, 1000));
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultPeriod_Is20()
    {
        var cci = new Cci();
        Assert.Equal(20, cci.Period);
    }

    [Fact]
    public void Constructor_CustomPeriod_IsStored()
    {
        var cci = new Cci(14);
        Assert.Equal(14, cci.Period);
    }

    [Fact]
    public void Constructor_PeriodLessThan2_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Cci(1));
    }

    [Fact]
    public void WarmupPeriod_IsDefault()
    {
        Assert.Equal(20, Cci.WarmupPeriod);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleBar_ReturnsZero()
    {
        var cci = new Cci(5);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        var result = cci.Update(bar);

        // With only one bar, CCI should be 0 (no deviation possible)
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Update_FlatMarket_ReturnsZero()
    {
        var cci = new Cci(5);
        var baseTime = DateTime.UtcNow;

        // All bars have same OHLC values - zero deviation
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(baseTime.AddDays(i), 100, 100, 100, 100, 1000);
            cci.Update(bar);
        }

        Assert.Equal(0, cci.Last.Value);
    }

    [Fact]
    public void Update_UpwardTrend_ReturnsPositive()
    {
        var cci = new Cci(5);

        // Feed upward trending bars
        for (int i = 0; i < 20; i++)
        {
            var bar = _bars[i];
            cci.Update(bar);
        }

        // CCI should be positive in uptrend
        Assert.True(cci.Last.Value > 0);
    }

    [Fact]
    public void Update_DownwardTrend_ReturnsNegative()
    {
        var cci = new Cci(5);
        var baseTime = DateTime.UtcNow;

        // Create downward trending bars
        for (int i = 0; i < 20; i++)
        {
            double high = 200 - i + 2;
            double low = 200 - i - 2;
            double close = 200 - i + 1;
            var bar = new TBar(baseTime.AddDays(i), 200 - i, high, low, close, 1000);
            cci.Update(bar);
        }

        // CCI should be negative in downtrend
        Assert.True(cci.Last.Value < 0);
    }

    #endregion

    #region IsHot Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var cci = new Cci(10);

        for (int i = 0; i < 5; i++)
        {
            cci.Update(_bars[i]);
        }

        Assert.False(cci.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var cci = new Cci(10);

        for (int i = 0; i < 15; i++)
        {
            cci.Update(_bars[i]);
        }

        Assert.True(cci.IsHot);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_BarCorrection_HandlesIsNewFalse()
    {
        var cci = new Cci(5);

        // Process some bars
        for (int i = 0; i < 10; i++)
        {
            cci.Update(_bars[i]);
        }

        double valueAfterBars = cci.Last.Value;

        // Simulate bar correction (isNew = false)
        var correctedBar = new TBar(
            _bars[9].Time,
            _bars[9].Open,
            _bars[9].High + 5, // Higher high
            _bars[9].Low,
            _bars[9].Close + 5, // Higher close
            _bars[9].Volume);

        cci.Update(correctedBar, isNew: false);

        // Value should change after correction
        Assert.NotEqual(valueAfterBars, cci.Last.Value);
    }

    [Fact]
    public void Update_RepeatedCorrection_ProducesSameResult()
    {
        var cci = new Cci(5);

        for (int i = 0; i < 10; i++)
        {
            cci.Update(_bars[i]);
        }

        var correctionBar = new TBar(DateTime.UtcNow, 105, 110, 100, 108, 1000);

        // Multiple corrections should give same result
        cci.Update(correctionBar, isNew: false);
        double firstCorrection = cci.Last.Value;

        cci.Update(correctionBar, isNew: false);
        double secondCorrection = cci.Last.Value;

        Assert.Equal(firstCorrection, secondCorrection);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var cci = new Cci(5);

        for (int i = 0; i < 20; i++)
        {
            cci.Update(_bars[i]);
        }

        cci.Reset();

        Assert.False(cci.IsHot);
        Assert.Equal(DateTime.MinValue.Ticks, cci.Last.Time);
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public void Batch_ReturnsCorrectCount()
    {
        var results = Cci.Batch(_bars, period: 10);
        Assert.Equal(_bars.Count, results.Count);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var (results, indicator) = Cci.Calculate(_bars, period: 14);

        Assert.Equal(_bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_TSeries_MatchesBatch()
    {
        var cci = new Cci(10);
        var results = cci.Update(_bars);
        var batchResults = Cci.Batch(_bars, 10);

        for (int i = 0; i < results.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, results[i].Value, 10);
        }
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_WarmupIndicator()
    {
        var cci = new Cci(10);

        cci.Prime(_bars);

        Assert.True(cci.IsHot);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var cci = new Cci(5);
        int eventCount = 0;

        cci.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 10; i++)
        {
            cci.Update(_bars[i]);
        }

        Assert.Equal(10, eventCount);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Update_NaNValue_UsesLastValid()
    {
        var cci = new Cci(5);

        // Add some valid bars
        for (int i = 0; i < 10; i++)
        {
            cci.Update(_bars[i]);
        }

        // Add bar with NaN
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000);
        var result = cci.Update(nanBar);

        // Should handle gracefully
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_InfinityValue_UsesLastValid()
    {
        var cci = new Cci(5);

        for (int i = 0; i < 10; i++)
        {
            cci.Update(_bars[i]);
        }

        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, 100, 1000);
        var result = cci.Update(infBar);

        Assert.True(double.IsFinite(result.Value));
    }

    #endregion
}
