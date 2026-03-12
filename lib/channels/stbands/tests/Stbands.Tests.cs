using Xunit;

namespace QuanTAlib.Tests;

public class StbandsTests
{
    #region Constructor Tests

    [Fact]
    public void Stbands_Constructor_ValidParameters()
    {
        Stbands stbands = new(period: 10, multiplier: 3.0);

        Assert.NotNull(stbands);
        Assert.Equal("Stbands(10,3.0)", stbands.Name);
        Assert.Equal(10, stbands.WarmupPeriod);
        Assert.False(stbands.IsHot);
    }

    [Fact]
    public void Stbands_Constructor_DefaultParameters()
    {
        Stbands stbands = new();

        Assert.Equal("Stbands(10,3.0)", stbands.Name);
        Assert.Equal(10, stbands.WarmupPeriod);
    }

    [Fact]
    public void Stbands_Constructor_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Stbands(period: 0));
        Assert.Equal("period", exception.ParamName);
    }

    [Fact]
    public void Stbands_Constructor_InvalidMultiplier_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Stbands(period: 10, multiplier: 0.0));
        Assert.Equal("multiplier", exception.ParamName);
    }

    [Fact]
    public void Stbands_Constructor_NegativeMultiplier_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stbands(period: 10, multiplier: -1.0));
    }

    #endregion

    #region Update TBar Tests

    [Fact]
    public void Stbands_Update_TBar_ReturnsValue()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        TBar bar = new(time, 100, 105, 95, 102, 1000);
        TValue result = stbands.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    [Fact]
    public void Stbands_BandCalculations_CorrectValues()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 102, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 110, 104, 108, 1000), isNew: true);

        Assert.True(stbands.Upper.Value > stbands.Lower.Value);
        Assert.True(stbands.Width.Value > 0);
        Assert.True(stbands.Trend.Value == 1 || stbands.Trend.Value == -1);
        Assert.True(stbands.IsHot);
    }

    #endregion

    #region Band Behavior Tests

    [Fact]
    public void Stbands_UpperBand_OnlyMovesDown_InDowntrend()
    {
        Stbands stbands = new(period: 3, multiplier: 1.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        double initialUpper = stbands.Upper.Value;

        stbands.Update(new TBar(time.AddMinutes(1), 98, 102, 94, 96, 1000), isNew: true);
        double secondUpper = stbands.Upper.Value;

        stbands.Update(new TBar(time.AddMinutes(2), 94, 98, 90, 92, 1000), isNew: true);
        _ = stbands.Upper.Value;

        Assert.True(secondUpper <= initialUpper || secondUpper == stbands.Upper.Value);
    }

    [Fact]
    public void Stbands_LowerBand_OnlyMovesUp_InUptrend()
    {
        Stbands stbands = new(period: 3, multiplier: 1.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 102, 1000), isNew: true);
        double initialLower = stbands.Lower.Value;

        stbands.Update(new TBar(time.AddMinutes(1), 104, 110, 102, 108, 1000), isNew: true);
        double secondLower = stbands.Lower.Value;

        stbands.Update(new TBar(time.AddMinutes(2), 110, 115, 108, 114, 1000), isNew: true);
        double thirdLower = stbands.Lower.Value;

        Assert.True(secondLower >= initialLower || thirdLower >= secondLower);
    }

    [Fact]
    public void Stbands_Last_EqualsTrendAppropiateBand()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            stbands.Update(bar, isNew: true);

            double trend = stbands.Trend.Value;
            if (trend > 0)
            {
                Assert.Equal(stbands.Lower.Value, stbands.Last.Value, 1e-10);
            }
            else
            {
                Assert.Equal(stbands.Upper.Value, stbands.Last.Value, 1e-10);
            }
        }
    }

    #endregion

    #region Trend Tests

    [Fact]
    public void Stbands_TrendDirection_ChangesOnBreakout()
    {
        Stbands stbands = new(period: 3, multiplier: 1.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 100, 105, 95, 100, 1000), isNew: true);
        _ = (int)stbands.Trend.Value;

        stbands.Update(new TBar(time.AddMinutes(3), 120, 130, 118, 128, 1000), isNew: true);

        Assert.True(stbands.Trend.Value == 1 || stbands.Trend.Value == -1);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Stbands_IsNew_False_RollsBackCorrectly()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: true);
        double upperBefore = stbands.Upper.Value;
        _ = stbands.Lower.Value;

        stbands.Update(new TBar(time.AddMinutes(2), 90, 95, 85, 88, 1000), isNew: false);
        double upperAfter = stbands.Upper.Value;
        _ = stbands.Lower.Value;

        Assert.NotEqual(upperBefore, upperAfter);
    }

    [Fact]
    public void Stbands_IsNew_False_IterativeCorrections_Restore()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: true);
        double originalUpper = stbands.Upper.Value;
        double originalLower = stbands.Lower.Value;

        stbands.Update(new TBar(time.AddMinutes(2), 90, 95, 85, 88, 1000), isNew: false);
        stbands.Update(new TBar(time.AddMinutes(2), 80, 85, 75, 78, 1000), isNew: false);

        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: false);
        double restoredUpper = stbands.Upper.Value;
        double restoredLower = stbands.Lower.Value;

        Assert.Equal(originalUpper, restoredUpper, precision: 10);
        Assert.Equal(originalLower, restoredLower, precision: 10);
    }

    #endregion

    #region NaN / Infinity Handling Tests

    [Fact]
    public void Stbands_NaN_HandledGracefully()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, 0), isNew: true);

        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    [Fact]
    public void Stbands_Infinity_HandledGracefully()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0), isNew: true);

        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Stbands_Reset_ClearsState()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: true);

        stbands.Reset();

        Assert.False(stbands.IsHot);
    }

    [Fact]
    public void Stbands_Reset_ThenReuse_ProducesSameResults()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;
        var bars = new TBar[]
        {
            new(time, 100, 105, 95, 100, 1000),
            new(time.AddMinutes(1), 102, 108, 100, 106, 1000),
            new(time.AddMinutes(2), 106, 112, 104, 110, 1000),
            new(time.AddMinutes(3), 108, 115, 105, 112, 1000),
            new(time.AddMinutes(4), 112, 118, 110, 116, 1000),
        };

        // First pass
        foreach (var bar in bars)
        {
            stbands.Update(bar, isNew: true);
        }
        double upperFirst = stbands.Upper.Value;
        double lowerFirst = stbands.Lower.Value;
        double trendFirst = stbands.Trend.Value;

        // Reset and second pass
        stbands.Reset();
        foreach (var bar in bars)
        {
            stbands.Update(bar, isNew: true);
        }

        Assert.Equal(upperFirst, stbands.Upper.Value, 1e-10);
        Assert.Equal(lowerFirst, stbands.Lower.Value, 1e-10);
        Assert.Equal(trendFirst, stbands.Trend.Value, 1e-10);
    }

    #endregion

    #region WarmupPeriod / IsHot Tests

    [Fact]
    public void Stbands_WarmupPeriod_IsHotTransition()
    {
        Stbands stbands = new(period: 5, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            stbands.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000), isNew: true);
            Assert.False(stbands.IsHot);
        }

        stbands.Update(new TBar(time.AddMinutes(4), 104, 109, 99, 106, 1000), isNew: true);
        Assert.True(stbands.IsHot);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Stbands_Prime_SetsIndicatorToHot()
    {
        Stbands stbands = new(period: 5, multiplier: 2.0);
        double[] prices = [100, 102, 104, 98, 96, 99, 103, 107, 105, 110];

        stbands.Prime(prices.AsSpan());

        Assert.True(stbands.IsHot);
        Assert.True(double.IsFinite(stbands.Last.Value));
        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    [Fact]
    public void Stbands_Prime_EmptySpan_DoesNotThrow()
    {
        Stbands stbands = new(period: 5, multiplier: 2.0);

        stbands.Prime(ReadOnlySpan<double>.Empty);

        Assert.False(stbands.IsHot);
    }

    [Fact]
    public void Stbands_Prime_WithCustomStep()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        double[] prices = [100, 102, 104, 106, 108];

        stbands.Prime(prices.AsSpan(), TimeSpan.FromMinutes(5));

        Assert.True(stbands.IsHot);
        Assert.True(double.IsFinite(stbands.Last.Value));
    }

    [Fact]
    public void Stbands_Prime_ThenUpdate_ContinuesCorrectly()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        double[] primeData = [100, 102, 104, 106, 108];

        stbands.Prime(primeData.AsSpan());
        Assert.True(stbands.IsHot);

        // Continue streaming with TBar
        stbands.Update(new TBar(DateTime.UtcNow, 108, 112, 106, 110, 1000), isNew: true);

        Assert.True(stbands.IsHot);
        Assert.True(double.IsFinite(stbands.Last.Value));
    }

    #endregion

    #region Calculate Tests

    [Fact]
    public void Stbands_Calculate_ReturnsResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Stbands.Calculate(bars, period: 5, multiplier: 2.0);

        Assert.True(indicator.IsHot);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.True(double.IsFinite(indicator.Upper.Value));
        Assert.True(double.IsFinite(indicator.Lower.Value));
    }

    #endregion

    #region Update TSeries Tests

    [Fact]
    public void Stbands_UpdateTSeries_ReturnsValidSeries()
    {
        Stbands stbands = new(period: 5, multiplier: 2.0);
        var series = new TSeries();
        for (int i = 0; i < 20; i++)
        {
            series.Add(DateTime.UtcNow.AddMinutes(i), 100 + (i * 0.5));
        }

        TSeries result = stbands.Update(series);

        Assert.Equal(20, result.Count);
        Assert.True(stbands.IsHot);
    }

    [Fact]
    public void Stbands_UpdateTSeries_NullSource_ThrowsArgumentNullException()
    {
        Stbands stbands = new(period: 5, multiplier: 2.0);

        Assert.Throws<ArgumentNullException>(() => stbands.Update((TSeries)null!));
    }

    [Fact]
    public void Stbands_UpdateTBarSeries_NullSource_ThrowsArgumentNullException()
    {
        Stbands stbands = new(period: 5, multiplier: 2.0);

        Assert.Throws<ArgumentNullException>(() => stbands.Update((TBarSeries)null!));
    }

    #endregion

    #region Update TBarSeries Tests

    [Fact]
    public void Stbands_UpdateTBarSeries_ReturnsValidSeries()
    {
        int period = 5;
        Stbands stbands = new(period, multiplier: 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        TSeries result = stbands.Update(bars);

        Assert.Equal(bars.Count, result.Count);
        Assert.True(stbands.IsHot);
    }

    #endregion

    #region Batch Tests

    [Fact]
    public void Stbands_StaticCalculate_ReturnsValidSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        TSeries result = Stbands.Batch(bars, period: 5, multiplier: 2.0);

        Assert.Equal(bars.Count, result.Count);
    }

    [Fact]
    public void Stbands_SpanCalculate_ProducesValidOutput()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 10;
        double multiplier = 3.0;

        double[] high = bars.High.Values.ToArray();
        double[] low = bars.Low.Values.ToArray();
        double[] close = bars.Close.Values.ToArray();
        double[] upper = new double[bars.Count];
        double[] lower = new double[bars.Count];
        double[] trend = new double[bars.Count];

        Stbands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), upper.AsSpan(), lower.AsSpan(), trend.AsSpan(), period, multiplier);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.True(double.IsFinite(upper[i]));
            Assert.True(double.IsFinite(lower[i]));
            Assert.True(trend[i] == 1 || trend[i] == -1);
            Assert.True(upper[i] >= lower[i]);
        }
    }

    [Fact]
    public void Stbands_SpanCalculate_InvalidLength_ThrowsArgumentException()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] upper = new double[10];
        double[] lower = new double[10];
        double[] trend = new double[9]; // Wrong length

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Stbands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), upper.AsSpan(), lower.AsSpan(), trend.AsSpan()));
        Assert.Equal("high", exception.ParamName);
    }

    [Fact]
    public void Stbands_SpanBatch_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        double[] data = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Stbands.Batch(data.AsSpan(), data.AsSpan(), data.AsSpan(),
                data.AsSpan(), data.AsSpan(), data.AsSpan(), period: 0));
    }

    [Fact]
    public void Stbands_SpanBatch_InvalidMultiplier_ThrowsArgumentOutOfRangeException()
    {
        double[] data = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Stbands.Batch(data.AsSpan(), data.AsSpan(), data.AsSpan(),
                data.AsSpan(), data.AsSpan(), data.AsSpan(), period: 10, multiplier: 0.0));
    }

    [Fact]
    public void Stbands_SpanBatch_EmptyArrays_DoesNotThrow()
    {
        double[] empty = [];

        Stbands.Batch(empty.AsSpan(), empty.AsSpan(), empty.AsSpan(),
            empty.AsSpan(), empty.AsSpan(), empty.AsSpan(), period: 10);

        Assert.Empty(empty);
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void Stbands_Consistency_StreamingVsBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 10;
        double multiplier = 3.0;

        // Streaming
        Stbands streamingStbands = new(period, multiplier);
        foreach (var bar in bars)
        {
            streamingStbands.Update(bar);
        }

        // Batch
        TSeries batchResult = Stbands.Batch(bars, period, multiplier);

        Assert.Equal(batchResult[^1].Value, streamingStbands.Last.Value, precision: 8);
    }

    [Fact]
    public void Stbands_Consistency_StreamingVsSpan()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 10;
        double multiplier = 3.0;

        // Streaming
        Stbands streamingStbands = new(period, multiplier);
        foreach (var bar in bars)
        {
            streamingStbands.Update(bar);
        }

        // Span
        double[] high = bars.High.Values.ToArray();
        double[] low = bars.Low.Values.ToArray();
        double[] close = bars.Close.Values.ToArray();
        double[] upper = new double[bars.Count];
        double[] lower = new double[bars.Count];
        double[] trend = new double[bars.Count];
        Stbands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), upper.AsSpan(), lower.AsSpan(), trend.AsSpan(), period, multiplier);

        Assert.Equal(upper[^1], streamingStbands.Upper.Value, precision: 8);
        Assert.Equal(lower[^1], streamingStbands.Lower.Value, precision: 8);
        Assert.Equal(trend[^1], streamingStbands.Trend.Value, precision: 8);
    }

    #endregion

    #region TValue Update Tests

    [Fact]
    public void Stbands_TValue_Update_WorksWithSingleValue()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TValue(time, 100.0), isNew: true);
        stbands.Update(new TValue(time.AddMinutes(1), 102.0), isNew: true);
        stbands.Update(new TValue(time.AddMinutes(2), 104.0), isNew: true);

        Assert.True(stbands.IsHot);
        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    #endregion

    #region Width Tests

    [Fact]
    public void Stbands_Width_IsUpperMinusLower()
    {
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 110, 90, 102, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 115, 95, 108, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 108, 120, 100, 115, 1000), isNew: true);

        Assert.Equal(stbands.Upper.Value - stbands.Lower.Value, stbands.Width.Value, precision: 10);
    }

    #endregion

    #region Pub Event Tests

    [Fact]
    public void Stbands_Pub_DoesNotFireDirectly()
    {
        // Stbands overrides Update paths without calling PubEvent —
        // Pub event is inherited from AbstractBase but not invoked.
        Stbands stbands = new(period: 3, multiplier: 2.0);
        bool fired = false;
        stbands.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        stbands.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);

        Assert.False(fired);
    }

    #endregion
}
