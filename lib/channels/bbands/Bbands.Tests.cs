using Xunit;

namespace QuanTAlib.Tests;

public class BbandsTests
{
    [Fact]
    public void Bbands_Constructor_ValidParameters()
    {
        // Arrange & Act
        Bbands bbands = new(period: 20, multiplier: 2.0);

        // Assert
        Assert.NotNull(bbands);
        Assert.Equal("Bbands(20,2.0)", bbands.Name);
        Assert.Equal(20, bbands.WarmupPeriod);
        Assert.False(bbands.IsHot);
    }

    [Fact]
    public void Bbands_Constructor_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        // Arrange, Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Bbands(period: 1));
        Assert.Equal("period", exception.ParamName);
    }

    [Fact]
    public void Bbands_Constructor_InvalidMultiplier_ThrowsArgumentOutOfRangeException()
    {
        // Arrange, Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Bbands(period: 20, multiplier: 0.05));
        Assert.Equal("multiplier", exception.ParamName);
    }

    [Fact]
    public void Bbands_Update_ReturnsCorrectMiddleBand()
    {
        // Arrange
        Bbands bbands = new(period: 5, multiplier: 2.0);
        double[] prices = [10.0, 11.0, 12.0, 13.0, 14.0, 15.0];
        DateTime time = DateTime.UtcNow;

        // Act
        TValue result = default;
        for (int i = 0; i < prices.Length; i++)
        {
            result = bbands.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // Assert - Middle should be SMA(5) = (11+12+13+14+15)/5 = 13.0
        Assert.Equal(13.0, result.Value, precision: 10);
        Assert.True(bbands.IsHot);
    }

    [Fact]
    public void Bbands_BandCalculations_CorrectValues()
    {
        // Arrange
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;
        double[] prices = [10.0, 12.0, 14.0];

        // Act
        for (int i = 0; i < prices.Length; i++)
        {
            bbands.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // Assert
        // SMA = (10+12+14)/3 = 12.0
        Assert.Equal(12.0, bbands.Middle.Value, precision: 10);

        // StdDev = sqrt(((10-12)^2 + (12-12)^2 + (14-12)^2) / 3) = sqrt(8/3) ≈ 1.6329931618554521
        double expectedStdDev = Math.Sqrt(8.0 / 3.0);
        double expectedUpper = 12.0 + (2.0 * expectedStdDev);
        double expectedLower = 12.0 - (2.0 * expectedStdDev);

        Assert.Equal(expectedUpper, bbands.Upper.Value, precision: 10);
        Assert.Equal(expectedLower, bbands.Lower.Value, precision: 10);
        Assert.Equal(expectedUpper - expectedLower, bbands.Width.Value, precision: 10);
    }

    [Fact]
    public void Bbands_PercentB_CorrectCalculation()
    {
        // Arrange
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;
        double[] prices = [10.0, 12.0, 14.0, 13.0];

        // Act
        for (int i = 0; i < prices.Length; i++)
        {
            bbands.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // Assert
        // For the last value (13.0) with window [12.0, 14.0, 13.0]
        // SMA = 13.0, StdDev = sqrt(2/3), Lower ≈ 11.367, Upper ≈ 14.633
        // %B = (13.0 - Lower) / (Upper - Lower)
        double percentB = bbands.PercentB.Value;
        Assert.True(percentB >= 0.0 && percentB <= 1.0);
    }

    [Fact]
    public void Bbands_IsNew_False_RollsBackCorrectly()
    {
        // Arrange
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act
        bbands.Update(new TValue(time, 10.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(1), 12.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(2), 14.0), isNew: true);
        double middleBefore = bbands.Middle.Value;

        bbands.Update(new TValue(time.AddSeconds(2), 15.0), isNew: false);
        double middleAfter = bbands.Middle.Value;

        // Assert - Value should change due to replacement
        Assert.NotEqual(middleBefore, middleAfter);
    }

    [Fact]
    public void Bbands_NaN_HandledGracefully()
    {
        // Arrange
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act
        bbands.Update(new TValue(time, 10.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(1), 12.0), isNew: true);

        bbands.Update(new TValue(time.AddSeconds(2), double.NaN), isNew: true);
        double afterNaN = bbands.Middle.Value;

        // Assert - Should substitute last valid value
        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Bbands_Reset_ClearsState()
    {
        // Arrange
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbands.Update(new TValue(time, 10.0));
        bbands.Update(new TValue(time.AddSeconds(1), 12.0));
        bbands.Update(new TValue(time.AddSeconds(2), 14.0));

        // Act
        bbands.Reset();

        // Assert
        Assert.False(bbands.IsHot);
    }

    [Fact]
    public void Bbands_WarmupPeriod_IsHotTransition()
    {
        // Arrange
        Bbands bbands = new(period: 5, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        // Act & Assert
        for (int i = 0; i < 4; i++)
        {
            bbands.Update(new TValue(time.AddSeconds(i), 10.0 + i));
            Assert.False(bbands.IsHot);
        }

        bbands.Update(new TValue(time.AddSeconds(4), 14.0));
        Assert.True(bbands.IsHot);
    }

    [Fact]
    public void Bbands_UpdateTSeries_ReturnsValidSeries()
    {
        // Arrange
        int period = 5;
        Bbands bbands = new(period, multiplier: 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Act
        TSeries result = bbands.Update(source);

        // Assert
        Assert.Equal(source.Count, result.Count);
        Assert.True(bbands.IsHot);

        // Verify last values match streaming
        Bbands streamingBbands = new(period, multiplier: 2.0);
        for (int i = Math.Max(0, source.Count - period); i < source.Count; i++)
        {
            streamingBbands.Update(source[i], isNew: true);
        }
        Assert.Equal(streamingBbands.Middle.Value, result[^1].Value, precision: 10);
    }

    [Fact]
    public void Bbands_StaticCalculate_ReturnsValidSeries()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Act
        TSeries result = Bbands.Calculate(source, period: 5, multiplier: 2.0);

        // Assert
        Assert.Equal(source.Count, result.Count);
        // Note: Static Calculate doesn't set IsHot on any instance, just returns the series
    }

    [Fact]
    public void Bbands_SpanCalculate_MatchesTSeries()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        int period = 20;
        double multiplier = 2.0;

        // Act - Copy to arrays before Span usage to avoid ref local issues
        double[] sourceArray = source.Values.ToArray();
        double[] middleArray = new double[source.Count];
        double[] upperArray = new double[source.Count];
        double[] lowerArray = new double[source.Count];
        Bbands.Calculate(sourceArray.AsSpan(), middleArray, upperArray, lowerArray, period, multiplier);

        TSeries seriesResult = Bbands.Calculate(source, period, multiplier);

        // Assert - Compare last 10 values
        for (int i = source.Count - 10; i < source.Count; i++)
        {
            Assert.Equal(seriesResult[i].Value, middleArray[i], precision: 10);
        }
    }

    [Fact]
    public void Bbands_SpanCalculate_InvalidLength_ThrowsArgumentException()
    {
        // Arrange
        double[] sourceArr = new double[10];
        double[] middleArr = new double[10];
        double[] upperArr = new double[10];
        double[] lowerArr = new double[9]; // Wrong length

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Bbands.Calculate(sourceArr.AsSpan(), middleArr.AsSpan(), upperArr.AsSpan(), lowerArr.AsSpan()));
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void Bbands_Chainability_WorksCorrectly()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        var pubSource = new TSeries();
        Bbands bbands1 = new(pubSource, period: 5, multiplier: 2.0);
        Bbands bbands2 = new(bbands1, period: 3, multiplier: 1.5);

        // Act
        bool eventFired = false;
        bbands2.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        foreach (var item in source)
        {
            pubSource.Add(item);
        }

        // Assert
        Assert.True(eventFired);
        Assert.True(bbands2.IsHot);
    }

    [Fact]
    public void Bbands_Consistency_StreamingVsBatchVsSpan()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        int period = 20;
        double multiplier = 2.0;

        // Streaming
        Bbands streamingBbands = new(period, multiplier);
        foreach (var item in source)
        {
            streamingBbands.Update(item);
        }

        // Batch
        TSeries batchResult = Bbands.Calculate(source, period, multiplier);

        // Span - Copy arrays before using to avoid ref local lambda issue
        double[] sourceArray = source.Values.ToArray();
        double[] middleArray = new double[source.Count];
        double[] upperArray = new double[source.Count];
        double[] lowerArray = new double[source.Count];
        Bbands.Calculate(sourceArray.AsSpan(), middleArray, upperArray, lowerArray, period, multiplier);

        // Assert - Compare last 50 values (streaming only has last value)
        Assert.Equal(batchResult[^1].Value, streamingBbands.Middle.Value, precision: 8);
        Assert.Equal(middleArray[^1], streamingBbands.Middle.Value, precision: 8);
    }
}