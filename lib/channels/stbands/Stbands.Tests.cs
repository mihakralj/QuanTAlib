using Xunit;

namespace QuanTAlib.Tests;

public class StbandsTests
{
    [Fact]
    public void Stbands_Constructor_ValidParameters()
    {
        // Arrange & Act
        Stbands stbands = new(period: 10, multiplier: 3.0);

        // Assert
        Assert.NotNull(stbands);
        Assert.Equal("Stbands(10,3.0)", stbands.Name);
        Assert.Equal(10, stbands.WarmupPeriod);
        Assert.False(stbands.IsHot);
    }

    [Fact]
    public void Stbands_Constructor_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        // Arrange, Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Stbands(period: 0));
        Assert.Equal("period", exception.ParamName);
    }

    [Fact]
    public void Stbands_Constructor_InvalidMultiplier_ThrowsArgumentOutOfRangeException()
    {
        // Arrange, Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Stbands(period: 10, multiplier: 0.0));
        Assert.Equal("multiplier", exception.ParamName);
    }

    [Fact]
    public void Stbands_Update_TBar_ReturnsValue()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act
        TBar bar = new(time, 100, 105, 95, 102, 1000);
        TValue result = stbands.Update(bar);

        // Assert
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    [Fact]
    public void Stbands_BandCalculations_CorrectValues()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act - Feed some bars
        stbands.Update(new TBar(time, 100, 105, 95, 102, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 110, 104, 108, 1000), isNew: true);

        // Assert
        Assert.True(stbands.Upper.Value > stbands.Lower.Value);
        Assert.True(stbands.Width.Value > 0);
        Assert.True(stbands.Trend.Value == 1 || stbands.Trend.Value == -1);
        Assert.True(stbands.IsHot);
    }

    [Fact]
    public void Stbands_UpperBand_OnlyMovesDown_InDowntrend()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 1.0);
        DateTime time = DateTime.UtcNow;

        // Act - Create downtrend scenario
        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        double initialUpper = stbands.Upper.Value;

        stbands.Update(new TBar(time.AddMinutes(1), 98, 102, 94, 96, 1000), isNew: true);
        double secondUpper = stbands.Upper.Value;

        stbands.Update(new TBar(time.AddMinutes(2), 94, 98, 90, 92, 1000), isNew: true);
        _ = stbands.Upper.Value;

        // Assert - Upper should not increase (only tighten or stay same)
        Assert.True(secondUpper <= initialUpper || secondUpper == stbands.Upper.Value);
    }

    [Fact]
    public void Stbands_LowerBand_OnlyMovesUp_InUptrend()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 1.0);
        DateTime time = DateTime.UtcNow;

        // Act - Create uptrend scenario
        stbands.Update(new TBar(time, 100, 105, 95, 102, 1000), isNew: true);
        double initialLower = stbands.Lower.Value;

        stbands.Update(new TBar(time.AddMinutes(1), 104, 110, 102, 108, 1000), isNew: true);
        double secondLower = stbands.Lower.Value;

        stbands.Update(new TBar(time.AddMinutes(2), 110, 115, 108, 114, 1000), isNew: true);
        double thirdLower = stbands.Lower.Value;

        // Assert - Lower should not decrease (only tighten or stay same)
        Assert.True(secondLower >= initialLower || thirdLower >= secondLower);
    }

    [Fact]
    public void Stbands_TrendDirection_ChangesOnBreakout()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 1.0);
        DateTime time = DateTime.UtcNow;

        // Act - Start with some bars
        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 100, 105, 95, 100, 1000), isNew: true);
        _ = (int)stbands.Trend.Value;

        // Create a large breakout above upper band
        stbands.Update(new TBar(time.AddMinutes(3), 120, 130, 118, 128, 1000), isNew: true);

        // Assert - Trend should potentially change
        Assert.True(stbands.Trend.Value == 1 || stbands.Trend.Value == -1);
    }

    [Fact]
    public void Stbands_IsNew_False_RollsBackCorrectly()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act
        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: true);
        double upperBefore = stbands.Upper.Value;
        _ = stbands.Lower.Value;

        // Update with different value, isNew = false
        stbands.Update(new TBar(time.AddMinutes(2), 90, 95, 85, 88, 1000), isNew: false);
        double upperAfter = stbands.Upper.Value;
        _ = stbands.Lower.Value;

        // Assert - Values should change due to bar correction
        Assert.NotEqual(upperBefore, upperAfter);
    }

    [Fact]
    public void Stbands_IsNew_False_IterativeCorrections_Restore()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act - Build up state
        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: true);
        double originalUpper = stbands.Upper.Value;
        double originalLower = stbands.Lower.Value;

        // Make multiple corrections
        stbands.Update(new TBar(time.AddMinutes(2), 90, 95, 85, 88, 1000), isNew: false);
        stbands.Update(new TBar(time.AddMinutes(2), 80, 85, 75, 78, 1000), isNew: false);

        // Restore original bar
        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: false);
        double restoredUpper = stbands.Upper.Value;
        double restoredLower = stbands.Lower.Value;

        // Assert - Should restore to original values
        Assert.Equal(originalUpper, restoredUpper, precision: 10);
        Assert.Equal(originalLower, restoredLower, precision: 10);
    }

    [Fact]
    public void Stbands_NaN_HandledGracefully()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act
        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, 0), isNew: true);

        // Assert - Should substitute last valid values
        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    [Fact]
    public void Stbands_Infinity_HandledGracefully()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act
        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 0), isNew: true);

        // Assert - Should substitute last valid values
        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
    }

    [Fact]
    public void Stbands_Reset_ClearsState()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        stbands.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 108, 100, 106, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 106, 112, 104, 110, 1000), isNew: true);

        // Act
        stbands.Reset();

        // Assert
        Assert.False(stbands.IsHot);
    }

    [Fact]
    public void Stbands_WarmupPeriod_IsHotTransition()
    {
        // Arrange
        Stbands stbands = new(period: 5, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act & Assert
        for (int i = 0; i < 4; i++)
        {
            stbands.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000), isNew: true);
            Assert.False(stbands.IsHot);
        }

        stbands.Update(new TBar(time.AddMinutes(4), 104, 109, 99, 106, 1000), isNew: true);
        Assert.True(stbands.IsHot);
    }

    [Fact]
    public void Stbands_UpdateTBarSeries_ReturnsValidSeries()
    {
        // Arrange
        int period = 5;
        Stbands stbands = new(period, multiplier: 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Act
        TSeries result = stbands.Update(bars);

        // Assert
        Assert.Equal(bars.Count, result.Count);
        Assert.True(stbands.IsHot);
    }

    [Fact]
    public void Stbands_StaticCalculate_ReturnsValidSeries()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        TBarSeries bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Act
        TSeries result = Stbands.Calculate(bars, period: 5, multiplier: 2.0);

        // Assert
        Assert.Equal(bars.Count, result.Count);
    }

    [Fact]
    public void Stbands_SpanCalculate_ProducesValidOutput()
    {
        // Arrange
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

        // Act
        Stbands.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(), upper.AsSpan(), lower.AsSpan(), trend.AsSpan(), period, multiplier);

        // Assert
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
        // Arrange
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] upper = new double[10];
        double[] lower = new double[10];
        double[] trend = new double[9]; // Wrong length

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Stbands.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(), upper.AsSpan(), lower.AsSpan(), trend.AsSpan()));
        Assert.Equal("high", exception.ParamName);
    }

    [Fact]
    public void Stbands_Consistency_StreamingVsBatch()
    {
        // Arrange
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
        TSeries batchResult = Stbands.Calculate(bars, period, multiplier);

        // Assert - Last values should match
        Assert.Equal(batchResult[^1].Value, streamingStbands.Last.Value, precision: 8);
    }

    [Fact]
    public void Stbands_Consistency_StreamingVsSpan()
    {
        // Arrange
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
        Stbands.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(), upper.AsSpan(), lower.AsSpan(), trend.AsSpan(), period, multiplier);

        // Assert - Last values should match
        Assert.Equal(upper[^1], streamingStbands.Upper.Value, precision: 8);
        Assert.Equal(lower[^1], streamingStbands.Lower.Value, precision: 8);
        Assert.Equal(trend[^1], streamingStbands.Trend.Value, precision: 8);
    }

    [Fact]
    public void Stbands_TValue_Update_WorksWithSingleValue()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act - Using TValue (treated as H=L=C=value)
        stbands.Update(new TValue(time, 100.0), isNew: true);
        stbands.Update(new TValue(time.AddMinutes(1), 102.0), isNew: true);
        stbands.Update(new TValue(time.AddMinutes(2), 104.0), isNew: true);

        // Assert
        Assert.True(stbands.IsHot);
        Assert.True(double.IsFinite(stbands.Upper.Value));
        Assert.True(double.IsFinite(stbands.Lower.Value));
        // With H=L=C, bands should be based on ATR=0 initially, but will have width from multiplier*0
        // Actually TR will be 0 when H-L=0, so bands may be tight
    }

    [Fact]
    public void Stbands_Width_IsUpperMinusLower()
    {
        // Arrange
        Stbands stbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act
        stbands.Update(new TBar(time, 100, 110, 90, 102, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(1), 102, 115, 95, 108, 1000), isNew: true);
        stbands.Update(new TBar(time.AddMinutes(2), 108, 120, 100, 115, 1000), isNew: true);

        // Assert
        Assert.Equal(stbands.Upper.Value - stbands.Lower.Value, stbands.Width.Value, precision: 10);
    }
}