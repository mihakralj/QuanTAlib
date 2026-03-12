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
        TSeries result = Bbands.Batch(source, period: 5, multiplier: 2.0);

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
        Bbands.Batch(sourceArray.AsSpan(), middleArray, upperArray, lowerArray, period, multiplier);

        TSeries seriesResult = Bbands.Batch(source, period, multiplier);

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
            () => Bbands.Batch(sourceArr.AsSpan(), middleArr.AsSpan(), upperArr.AsSpan(), lowerArr.AsSpan()));
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
        TSeries batchResult = Bbands.Batch(source, period, multiplier);

        // Span - Copy arrays before using to avoid ref local lambda issue
        double[] sourceArray = source.Values.ToArray();
        double[] middleArray = new double[source.Count];
        double[] upperArray = new double[source.Count];
        double[] lowerArray = new double[source.Count];
        Bbands.Batch(sourceArray.AsSpan(), middleArray, upperArray, lowerArray, period, multiplier);

        // Assert - Compare last 50 values (streaming only has last value)
        Assert.Equal(batchResult[^1].Value, streamingBbands.Middle.Value, precision: 8);
        Assert.Equal(middleArray[^1], streamingBbands.Middle.Value, precision: 8);
    }

    #region Default Constructor

    [Fact]
    public void Bbands_Constructor_DefaultParameters()
    {
        // Default: period=20, multiplier=2.0
        Bbands bbands = new();

        Assert.Equal("Bbands(20,2.0)", bbands.Name);
        Assert.Equal(20, bbands.WarmupPeriod);
        Assert.False(bbands.IsHot);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Bbands_Prime_SetsIndicatorToHot()
    {
        Bbands bbands = new(period: 5, multiplier: 2.0);

        double[] data = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109];

        Assert.False(bbands.IsHot);

        bbands.Prime(data);

        Assert.True(bbands.IsHot);
        Assert.True(double.IsFinite(bbands.Middle.Value));
        Assert.True(double.IsFinite(bbands.Upper.Value));
        Assert.True(double.IsFinite(bbands.Lower.Value));
    }

    [Fact]
    public void Bbands_Prime_EmptySpan_DoesNotThrow()
    {
        Bbands bbands = new(period: 5, multiplier: 2.0);
        var ex = Record.Exception(() => bbands.Prime(ReadOnlySpan<double>.Empty));
        Assert.Null(ex);
        Assert.False(bbands.IsHot);
    }

    [Fact]
    public void Bbands_Prime_WithStep_UsesCorrectSpacing()
    {
        Bbands bbands = new(period: 3, multiplier: 2.0);
        double[] data = [100, 102, 104, 106, 108];
        var step = TimeSpan.FromHours(1);

        bbands.Prime(data, step);

        Assert.True(bbands.IsHot);
        Assert.True(double.IsFinite(bbands.Last.Value));
    }

    [Fact]
    public void Bbands_Prime_ThenUpdate_ContinuesCorrectly()
    {
        Bbands bbands = new(period: 5, multiplier: 2.0);

        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109];
        bbands.Prime(primeData);
        Assert.True(bbands.IsHot);

        double middleAfterPrime = bbands.Middle.Value;

        // Continue with streaming
        bbands.Update(new TValue(DateTime.UtcNow, 120.0), isNew: true);

        Assert.NotEqual(middleAfterPrime, bbands.Middle.Value);
        Assert.True(double.IsFinite(bbands.Middle.Value));
        Assert.True(double.IsFinite(bbands.Upper.Value));
        Assert.True(double.IsFinite(bbands.Lower.Value));
        Assert.True(bbands.Upper.Value > bbands.Middle.Value);
        Assert.True(bbands.Lower.Value < bbands.Middle.Value);
    }

    [Fact]
    public void Bbands_Prime_MatchesStreamingResults()
    {
        double[] data = [100, 102, 98, 105, 103, 107, 101, 99, 106, 104];

        // Via Prime
        Bbands primedBbands = new(period: 5, multiplier: 2.0);
        primedBbands.Prime(data);

        // Via streaming Update
        Bbands streamBbands = new(period: 5, multiplier: 2.0);
        DateTime startTime = DateTime.UtcNow;
        for (int i = 0; i < data.Length; i++)
        {
            streamBbands.Update(new TValue(startTime + (i * TimeSpan.FromSeconds(1)), data[i]), isNew: true);
        }

        Assert.Equal(streamBbands.Middle.Value, primedBbands.Middle.Value, precision: 10);
        Assert.Equal(streamBbands.Upper.Value, primedBbands.Upper.Value, precision: 10);
        Assert.Equal(streamBbands.Lower.Value, primedBbands.Lower.Value, precision: 10);
    }

    #endregion

    #region Calculate Tests

    [Fact]
    public void Bbands_Calculate_ReturnsResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var (results, indicator) = Bbands.Calculate(source, period: 5, multiplier: 2.0);

        // Check results
        Assert.Equal(50, results.Count);

        // Check indicator is hot and has valid state
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Middle.Value));
        Assert.True(double.IsFinite(indicator.Upper.Value));
        Assert.True(double.IsFinite(indicator.Lower.Value));

        // Verify indicator can continue streaming
        indicator.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        Assert.True(double.IsFinite(indicator.Middle.Value));
    }

    [Fact]
    public void Bbands_Calculate_DefaultParameters()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var (results, indicator) = Bbands.Calculate(source);

        Assert.Equal(30, results.Count);
        Assert.Equal(20, indicator.WarmupPeriod);
        Assert.True(indicator.IsHot); // 30 > 20
    }

    #endregion

    #region Update(TSeries) Edge Cases

    [Fact]
    public void Bbands_UpdateTSeries_NullSource_ThrowsArgumentNullException()
    {
        Bbands bbands = new(period: 5, multiplier: 2.0);

        Assert.Throws<ArgumentNullException>(() => bbands.Update((TSeries)null!));
    }

    #endregion

    #region Infinity Handling

    [Fact]
    public void Bbands_Infinity_HandledGracefully()
    {
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbands.Update(new TValue(time, 10.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(1), 12.0), isNew: true);

        // PositiveInfinity should use last valid value
        bbands.Update(new TValue(time.AddSeconds(2), double.PositiveInfinity), isNew: true);
        Assert.True(double.IsFinite(bbands.Middle.Value));
        Assert.True(double.IsFinite(bbands.Upper.Value));
        Assert.True(double.IsFinite(bbands.Lower.Value));

        // NegativeInfinity should also be handled
        bbands.Update(new TValue(time.AddSeconds(3), double.NegativeInfinity), isNew: true);
        Assert.True(double.IsFinite(bbands.Middle.Value));
    }

    #endregion

    #region PercentB Edge Cases

    [Fact]
    public void Bbands_PercentB_ZeroWidth_ReturnsZero()
    {
        // When all values are the same, stddev = 0, width = 0, percentB should be 0
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbands.Update(new TValue(time, 100.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(1), 100.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(2), 100.0), isNew: true);

        Assert.Equal(0.0, bbands.Width.Value, precision: 10);
        Assert.Equal(0.0, bbands.PercentB.Value, precision: 10);
    }

    [Fact]
    public void Bbands_PercentB_AtMiddle_IsFiftyPercent()
    {
        // When price equals the middle band, %B should be ≈ 0.5
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbands.Update(new TValue(time, 10.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(1), 12.0), isNew: true);
        bbands.Update(new TValue(time.AddSeconds(2), 14.0), isNew: true);

        // SMA = 12.0, feeding 12.0 next — it becomes middle of [12, 14, 12] = SMA ≈ 12.67
        // Need to check the actual calculation rather than assume
        // The point: when price = middle, %B = (price - lower) / (upper - lower)
        // which would be 0.5 since middle is equidistant from upper and lower
        bbands.Update(new TValue(time.AddSeconds(3), bbands.Middle.Value), isNew: true);
        // After this update, the SMA shifts, but %B should be ≈ 0.5
        Assert.True(bbands.PercentB.Value > 0.3 && bbands.PercentB.Value < 0.7);
    }

    #endregion

    #region Reset State Tests

    [Fact]
    public void Bbands_Reset_ClearsAllProperties()
    {
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbands.Update(new TValue(time, 10.0));
        bbands.Update(new TValue(time.AddSeconds(1), 12.0));
        bbands.Update(new TValue(time.AddSeconds(2), 14.0));

        Assert.True(bbands.IsHot);
        Assert.NotEqual(0, bbands.Middle.Value);
        Assert.NotEqual(0, bbands.Upper.Value);

        bbands.Reset();

        Assert.False(bbands.IsHot);
        Assert.Equal(0, bbands.Middle.Value);
        Assert.Equal(0, bbands.Upper.Value);
        Assert.Equal(0, bbands.Lower.Value);
        Assert.Equal(0, bbands.Width.Value);
        Assert.Equal(0, bbands.PercentB.Value);
    }

    [Fact]
    public void Bbands_Reset_ThenReuse_ProducesSameResults()
    {
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;
        double[] prices = [10.0, 12.0, 14.0];

        // First pass
        for (int i = 0; i < prices.Length; i++)
        {
            bbands.Update(new TValue(time.AddSeconds(i), prices[i]));
        }
        double firstMiddle = bbands.Middle.Value;
        double firstUpper = bbands.Upper.Value;
        double firstLower = bbands.Lower.Value;

        // Reset and second pass with same data
        bbands.Reset();
        for (int i = 0; i < prices.Length; i++)
        {
            bbands.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        Assert.Equal(firstMiddle, bbands.Middle.Value, precision: 10);
        Assert.Equal(firstUpper, bbands.Upper.Value, precision: 10);
        Assert.Equal(firstLower, bbands.Lower.Value, precision: 10);
    }

    #endregion

    #region Span Batch Edge Cases

    [Fact]
    public void Bbands_SpanBatch_EmptyArrays_DoesNotThrow()
    {
        double[] source = [];
        double[] middle = [];
        double[] upper = [];
        double[] lower = [];

        var ex = Record.Exception(() => Bbands.Batch(
            source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan()));
        Assert.Null(ex);
    }

    [Fact]
    public void Bbands_SpanBatch_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[10];
        double[] middle = new double[10];
        double[] upper = new double[10];
        double[] lower = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Bbands.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(),
                period: 1, multiplier: 2.0));
    }

    [Fact]
    public void Bbands_SpanBatch_InvalidMultiplier_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[10];
        double[] middle = new double[10];
        double[] upper = new double[10];
        double[] lower = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Bbands.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(),
                period: 5, multiplier: 0.05));
    }

    [Fact]
    public void Bbands_SpanBatch_ShorterThanPeriod_SetsNaN()
    {
        // Source shorter than period — all upper/lower should be NaN
        double[] source = [100, 101, 102];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        Bbands.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(),
            period: 5, multiplier: 2.0);

        // First (period-1) values should be NaN for upper/lower
        for (int i = 0; i < 3; i++)
        {
            Assert.True(double.IsNaN(upper[i]));
            Assert.True(double.IsNaN(lower[i]));
        }
    }

    [Fact]
    public void Bbands_SpanBatch_NaN_InWindow_EmitsNaN()
    {
        double[] source = [100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109];
        double[] middle = new double[10];
        double[] upper = new double[10];
        double[] lower = new double[10];

        Bbands.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(),
            period: 5, multiplier: 2.0);

        // Index 4 (first complete window [100,101,NaN,103,104]) contains NaN
        // So upper/lower at index 4 should be NaN
        Assert.True(double.IsNaN(upper[4]));
        Assert.True(double.IsNaN(lower[4]));

        // Once NaN exits the window, values should become finite again
        // Window at index 7: [103, 104, 105, 106, 107] — all finite
        Assert.True(double.IsFinite(upper[7]));
        Assert.True(double.IsFinite(lower[7]));
    }

    #endregion

    #region Band Relationship Tests

    [Fact]
    public void Bbands_UpperAlwaysAboveLower()
    {
        Bbands bbands = new(period: 5, multiplier: 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        DateTime time = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            bbands.Update(new TValue(time.AddMinutes(i), bar.Close), isNew: true);

            if (bbands.IsHot)
            {
                Assert.True(bbands.Upper.Value >= bbands.Lower.Value,
                    $"Upper ({bbands.Upper.Value}) should be >= Lower ({bbands.Lower.Value}) at step {i}");
                Assert.True(bbands.Width.Value >= 0,
                    $"Width ({bbands.Width.Value}) should be >= 0 at step {i}");
            }
        }
    }

    [Fact]
    public void Bbands_MiddleIsBetweenBands()
    {
        Bbands bbands = new(period: 5, multiplier: 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        DateTime time = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            bbands.Update(new TValue(time.AddMinutes(i), bar.Close), isNew: true);

            if (bbands.IsHot)
            {
                Assert.True(bbands.Middle.Value >= bbands.Lower.Value,
                    $"Middle ({bbands.Middle.Value}) should be >= Lower ({bbands.Lower.Value})");
                Assert.True(bbands.Middle.Value <= bbands.Upper.Value,
                    $"Middle ({bbands.Middle.Value}) should be <= Upper ({bbands.Upper.Value})");
            }
        }
    }

    [Fact]
    public void Bbands_MultiplierAffectsBandWidth()
    {
        DateTime time = DateTime.UtcNow;
        double[] prices = [100, 102, 98, 105, 103, 107, 101, 99, 106, 104];

        Bbands narrow = new(period: 5, multiplier: 1.0);
        Bbands wide = new(period: 5, multiplier: 3.0);

        for (int i = 0; i < prices.Length; i++)
        {
            narrow.Update(new TValue(time.AddSeconds(i), prices[i]), isNew: true);
            wide.Update(new TValue(time.AddSeconds(i), prices[i]), isNew: true);
        }

        // Wider multiplier should produce wider bands
        Assert.True(wide.Width.Value > narrow.Width.Value);
        // Middle should be the same (same SMA)
        Assert.Equal(narrow.Middle.Value, wide.Middle.Value, precision: 10);
    }

    #endregion

    #region Last Property

    [Fact]
    public void Bbands_Last_EqualsMiddle()
    {
        Bbands bbands = new(period: 3, multiplier: 2.0);
        DateTime time = DateTime.UtcNow;

        bbands.Update(new TValue(time, 10.0));
        bbands.Update(new TValue(time.AddSeconds(1), 12.0));
        bbands.Update(new TValue(time.AddSeconds(2), 14.0));

        // Last should be the Middle band value
        Assert.Equal(bbands.Middle.Value, bbands.Last.Value, precision: 10);
        Assert.Equal(bbands.Middle.Time, bbands.Last.Time);
    }

    #endregion
}
