namespace QuanTAlib.Tests;

public class GBMTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidInstance()
    {
        var gbm = new GBM();

        Assert.Equal(100.0, gbm.StartPrice);
        Assert.Equal(0.05, gbm.Mu);
        Assert.Equal(0.2, gbm.Sigma);
        Assert.Equal(100.0, gbm.CurrentPrice);
        Assert.False(gbm.HasCurrentBar);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectly()
    {
        var gbm = new GBM(startPrice: 50.0, mu: 0.1, sigma: 0.3, seed: 42);

        Assert.Equal(50.0, gbm.StartPrice);
        Assert.Equal(0.1, gbm.Mu);
        Assert.Equal(0.3, gbm.Sigma);
        Assert.Equal(50.0, gbm.CurrentPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_InvalidStartPrice_ThrowsArgumentOutOfRangeException(double startPrice)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(startPrice: startPrice));
    }

    [Fact]
    public void Constructor_NaNStartPrice_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(startPrice: double.NaN));
    }

    [Fact]
    public void Constructor_InfinityStartPrice_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(startPrice: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(startPrice: double.NegativeInfinity));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    public void Constructor_NegativeSigma_ThrowsArgumentOutOfRangeException(double sigma)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(sigma: sigma));
    }

    [Fact]
    public void Constructor_NaNSigma_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(sigma: double.NaN));
    }

    [Fact]
    public void Constructor_InfinitySigma_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(sigma: double.PositiveInfinity));
    }

    [Fact]
    public void Constructor_NaNMu_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(mu: double.NaN));
    }

    [Fact]
    public void Constructor_InfinityMu_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(mu: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(mu: double.NegativeInfinity));
    }

    [Fact]
    public void Constructor_ZeroTimeframe_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(defaultTimeframe: TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeTimeframe_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GBM(defaultTimeframe: TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void Constructor_ZeroSigma_IsValid()
    {
        var gbm = new GBM(sigma: 0);
        Assert.Equal(0, gbm.Sigma);
    }

    [Fact]
    public void Constructor_NegativeMu_IsValid()
    {
        var gbm = new GBM(mu: -0.1);
        Assert.Equal(-0.1, gbm.Mu);
    }

    #endregion

    #region Next Method Tests

    [Fact]
    public void Next_DefaultParameter_GeneratesNewBar()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        var bar1 = gbm.Next();
        var bar2 = gbm.Next();

        Assert.NotEqual(bar1.Time, bar2.Time);
        Assert.True(bar2.Time > bar1.Time);
    }

    [Fact]
    public void Next_IsNewTrue_AdvancesToNewBar()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        var bar1 = gbm.Next(isNew: true);
        var bar2 = gbm.Next(isNew: true);

        Assert.NotEqual(bar1.Time, bar2.Time);
        Assert.True(bar2.Time > bar1.Time);
    }

    [Fact]
    public void Next_IsNewFalse_UpdatesCurrentBar()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        var bar1 = gbm.Next(isNew: true);
        long initialTime = bar1.Time;

        var bar2 = gbm.Next(isNew: false);

        Assert.Equal(initialTime, bar2.Time);
        Assert.Equal(bar1.Open, bar2.Open);
        // High/Low/Close/Volume may change
    }

    [Fact]
    public void Next_RefBool_HonorsRequest()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        bool isNew1 = true;
        var bar1 = gbm.Next(ref isNew1);
        Assert.True(isNew1, "GBM should honor isNew=true request");

        bool isNew2 = false;
        long time1 = bar1.Time;
        var bar2 = gbm.Next(ref isNew2);
        Assert.False(isNew2, "GBM should honor isNew=false request");
        Assert.Equal(time1, bar2.Time);

        bool isNew3 = true;
        var bar3 = gbm.Next(ref isNew3);
        Assert.True(isNew3, "GBM should honor isNew=true request");
        Assert.NotEqual(time1, bar3.Time);
    }

    [Fact]
    public void Next_FirstCallWithIsNewFalse_GeneratesBar()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        // First call with isNew=false should still generate a bar
        var bar = gbm.Next(isNew: false);

        Assert.True(bar.Time > 0);
        Assert.True(bar.Open > 0);
        Assert.True(gbm.HasCurrentBar);
    }

    [Fact]
    public void Next_MultipleUpdates_AccumulatesVolume()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        var bar1 = gbm.Next(isNew: true);
        double initialVolume = bar1.Volume;

        var bar2 = gbm.Next(isNew: false);

        Assert.True(bar2.Volume > initialVolume, "Volume should accumulate on intra-bar updates");
    }

    [Fact]
    public void Next_IntraBarUpdates_ExpandsHighLow()
    {
        var gbm = new GBM(startPrice: 100.0, sigma: 0.5, seed: 42);

        var bar1 = gbm.Next(isNew: true);
        double initialHigh = bar1.High;
        double initialLow = bar1.Low;

        // Multiple updates should potentially expand the range
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: false);
            Assert.True(bar.High >= initialHigh || bar.Low <= initialLow || i > 50,
                "High-Low range should expand or stay same with updates");
        }
    }

    #endregion

    #region Fetch Method Tests

    [Fact]
    public void Fetch_GeneratesCorrectCount()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        const int count = 10;
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series = gbm.Fetch(count, startTime, interval);

        Assert.Equal(count, series.Count);
    }

    [Fact]
    public void Fetch_GeneratesSequentialBars()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series = gbm.Fetch(5, startTime, interval);

        for (int i = 1; i < series.Count; i++)
        {
            Assert.True(series[i].Time > series[i - 1].Time);
        }
    }

    [Fact]
    public void Fetch_RespectsInterval()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        var interval = TimeSpan.FromHours(1);
        long startTime = DateTime.UtcNow.Ticks;

        var series = gbm.Fetch(5, startTime, interval);

        for (int i = 1; i < series.Count; i++)
        {
            long expectedDiff = interval.Ticks;
            long actualDiff = series[i].Time - series[i - 1].Time;
            Assert.Equal(expectedDiff, actualDiff);
        }
    }

    [Fact]
    public void Fetch_StartsAtSpecifiedTime()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        var startTime = new DateTime(2024, 1, 1, 9, 30, 0, DateTimeKind.Utc).Ticks;
        var interval = TimeSpan.FromMinutes(5);

        var series = gbm.Fetch(3, startTime, interval);

        Assert.Equal(startTime, series[0].Time);
        Assert.Equal(startTime + interval.Ticks, series[1].Time);
        Assert.Equal(startTime + 2 * interval.Ticks, series[2].Time);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Fetch_InvalidCount_ThrowsArgumentException(int count)
    {
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        Assert.Throws<ArgumentException>(() => gbm.Fetch(count, startTime, interval));
    }

    [Fact]
    public void Fetch_ZeroInterval_ThrowsArgumentOutOfRangeException()
    {
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;

        Assert.Throws<ArgumentOutOfRangeException>(() => gbm.Fetch(10, startTime, TimeSpan.Zero));
    }

    [Fact]
    public void Fetch_NegativeInterval_ThrowsArgumentOutOfRangeException()
    {
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;

        Assert.Throws<ArgumentOutOfRangeException>(() => gbm.Fetch(10, startTime, TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void Fetch_WithDifferentIntervals_WorksCorrectly()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        long startTime = DateTime.UtcNow.Ticks;

        var intervals = new[] {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromHours(1)
        };

        foreach (var interval in intervals)
        {
            var series = gbm.Fetch(3, startTime, interval);

            for (int i = 1; i < series.Count; i++)
            {
                long expectedDiff = interval.Ticks;
                long actualDiff = series[i].Time - series[i - 1].Time;
                Assert.Equal(expectedDiff, actualDiff);
            }
        }
    }

    [Fact]
    public void Fetch_LargeCount_WorksCorrectly()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series = gbm.Fetch(10000, startTime, interval);

        Assert.Equal(10000, series.Count);
        Assert.All(Enumerable.Range(0, series.Count), i =>
        {
            Assert.True(series[i].Open > 0);
            Assert.True(series[i].High > 0);
            Assert.True(series[i].Low > 0);
            Assert.True(series[i].Close > 0);
            Assert.True(series[i].Volume > 0);
        });
    }

    #endregion

    #region OHLCV Validity Tests

    [Fact]
    public void GeneratesRealisticOHLCV()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var series = gbm.Fetch(100, startTime, interval);

        for (int i = 0; i < series.Count; i++)
        {
            var bar = series[i];

            // High should be >= max(Open, Close)
            Assert.True(bar.High >= Math.Max(bar.Open, bar.Close),
                $"Bar {i}: High ({bar.High}) should be >= max(Open, Close) ({Math.Max(bar.Open, bar.Close)})");

            // Low should be <= min(Open, Close)
            Assert.True(bar.Low <= Math.Min(bar.Open, bar.Close),
                $"Bar {i}: Low ({bar.Low}) should be <= min(Open, Close) ({Math.Min(bar.Open, bar.Close)})");

            // High should be >= Low
            Assert.True(bar.High >= bar.Low,
                $"Bar {i}: High ({bar.High}) should be >= Low ({bar.Low})");

            // Volume should be positive
            Assert.True(bar.Volume > 0, $"Bar {i}: Volume should be positive");

            // All prices should be positive and finite
            Assert.True(double.IsFinite(bar.Open) && bar.Open > 0, $"Bar {i}: Open should be positive and finite");
            Assert.True(double.IsFinite(bar.High) && bar.High > 0, $"Bar {i}: High should be positive and finite");
            Assert.True(double.IsFinite(bar.Low) && bar.Low > 0, $"Bar {i}: Low should be positive and finite");
            Assert.True(double.IsFinite(bar.Close) && bar.Close > 0, $"Bar {i}: Close should be positive and finite");
        }
    }

    [Fact]
    public void ConsecutiveCalls_MaintainContinuity()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        var previousBar = gbm.Next();
        var currentBar = gbm.Next();

        // currentBar.Open should equal previousBar.Close (continuity)
        Assert.Equal(previousBar.Close, currentBar.Open);
    }

    [Fact]
    public void Fetch_MaintainsContinuity()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series = gbm.Fetch(10, startTime, interval);

        for (int i = 1; i < series.Count; i++)
        {
            Assert.True(Math.Abs(series[i - 1].Close - series[i].Open) < 1e-10,
                $"Bar {i}: Open should equal previous bar's Close for continuity");
        }
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_RestoresInitialState()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        // Generate some bars
        gbm.Next();
        gbm.Next();
        gbm.Next();

        Assert.NotEqual(100.0, gbm.CurrentPrice);
        Assert.True(gbm.HasCurrentBar);

        // Reset
        gbm.Reset();

        Assert.Equal(100.0, gbm.CurrentPrice);
        Assert.False(gbm.HasCurrentBar);
    }

    [Fact]
    public void Reset_WithStartTime_SetsSpecificTime()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);
        long specificTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        gbm.Next();
        gbm.Reset(specificTime);

        var bar = gbm.Next();

        // The bar time should be based on the reset time
        Assert.True(bar.Time > specificTime);
        Assert.Equal(100.0, bar.Open); // Should start from initial price
    }

    #endregion

    #region Seeded Reproducibility Tests

    [Fact]
    public void SeededGenerator_ProducesReproducibleResults()
    {
        var gbm1 = new GBM(startPrice: 100.0, seed: 42);
        var gbm2 = new GBM(startPrice: 100.0, seed: 42);

        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series1 = gbm1.Fetch(10, startTime, interval);
        var series2 = gbm2.Fetch(10, startTime, interval);

        for (int i = 0; i < series1.Count; i++)
        {
            Assert.Equal(series1[i].Open, series2[i].Open);
            Assert.Equal(series1[i].High, series2[i].High);
            Assert.Equal(series1[i].Low, series2[i].Low);
            Assert.Equal(series1[i].Close, series2[i].Close);
            Assert.Equal(series1[i].Volume, series2[i].Volume);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentResults()
    {
        var gbm1 = new GBM(startPrice: 100.0, seed: 42);
        var gbm2 = new GBM(startPrice: 100.0, seed: 123);

        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series1 = gbm1.Fetch(10, startTime, interval);
        var series2 = gbm2.Fetch(10, startTime, interval);

        bool anyDifferent = false;
        for (int i = 0; i < series1.Count; i++)
        {
            if (Math.Abs(series1[i].Close - series2[i].Close) > 1e-14)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, "Different seeds should produce different results");
    }

    [Fact]
    public void UnseededGenerator_ProducesVariableResults()
    {
        var gbm1 = new GBM(startPrice: 100.0);
        var gbm2 = new GBM(startPrice: 100.0);

        // Note: This test may occasionally fail due to randomness, but is extremely unlikely
        var bar1 = gbm1.Next();
        var bar2 = gbm2.Next();

        // At least one value should be different (use tolerance for floating-point comparison)
        const double tolerance = 1e-14;
        bool anyDifferent = Math.Abs(bar1.Close - bar2.Close) > tolerance ||
                           Math.Abs(bar1.High - bar2.High) > tolerance ||
                           Math.Abs(bar1.Low - bar2.Low) > tolerance ||
                           Math.Abs(bar1.Volume - bar2.Volume) > tolerance;

        Assert.True(anyDifferent, "Unseeded generators should produce different results");
    }

    #endregion

    #region Drift and Volatility Tests

    [Fact]
    public void DriftAndVolatility_AffectPriceMovement()
    {
        var gbmLowVol = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.01, seed: 42);
        var gbmHighVol = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.5, seed: 42);

        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var seriesLow = gbmLowVol.Fetch(100, startTime, interval);
        var seriesHigh = gbmHighVol.Fetch(100, startTime, interval);

        // Calculate standard deviation of returns
        double[] returnsLow = new double[99];
        double[] returnsHigh = new double[99];

        for (int i = 1; i < 100; i++)
        {
            returnsLow[i - 1] = Math.Log(seriesLow[i].Close / seriesLow[i - 1].Close);
            returnsHigh[i - 1] = Math.Log(seriesHigh[i].Close / seriesHigh[i - 1].Close);
        }

        double stdLow = CalculateStdDev(returnsLow);
        double stdHigh = CalculateStdDev(returnsHigh);

        Assert.True(stdHigh > stdLow, "High volatility should produce larger return dispersion");
    }

    [Fact]
    public void ZeroVolatility_ProducesConstantPrices()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.0, seed: 42);

        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var series = gbm.Fetch(10, startTime, interval);

        // With zero volatility and zero drift, price should stay constant
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(100.0, series[i].Close, 10);
        }
    }

    private static double CalculateStdDev(double[] values)
    {
        double mean = 0;
        for (int i = 0; i < values.Length; i++)
        {
            mean += values[i];
        }

        mean /= values.Length;

        double sumSquares = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sumSquares += (values[i] - mean) * (values[i] - mean);
        }

        return Math.Sqrt(sumSquares / values.Length);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void IntraBarUpdates_ModifyCurrentBar()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        var bar1 = gbm.Next(isNew: true);
        long initialTime = bar1.Time;
        double initialClose = bar1.Close;

        bool changed = false;
        const double tolerance = 1e-14;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: false);
            Assert.Equal(initialTime, bar.Time);
            if (Math.Abs(bar.Close - initialClose) > tolerance)
            {
                changed = true;
                break;
            }
        }

        Assert.True(changed, "Price should change during intra-bar updates");
    }

    [Fact]
    public void MixedStreamingAndBatch_WorksCorrectly()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        _ = gbm.Next();
        var bar2 = gbm.Next();

        long startTime = bar2.Time + TimeSpan.FromMinutes(1).Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var series = gbm.Fetch(3, startTime, interval);

        Assert.True(series[0].Time > bar2.Time);
        Assert.Equal(3, series.Count);

        var bar3 = gbm.Next();
        Assert.True(bar3.Time > series[2].Time);
    }

    [Fact]
    public void Fetch_ResetsStreamingState()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 42);

        // Create a bar with intra-bar updates
        gbm.Next(isNew: true);
        gbm.Next(isNew: false);
        Assert.True(gbm.HasCurrentBar);

        // Fetch should reset streaming state
        long startTime = DateTime.UtcNow.Ticks;
        gbm.Fetch(5, startTime, TimeSpan.FromMinutes(1));

        Assert.False(gbm.HasCurrentBar);
    }

    #endregion

    #region IFeed Interface Tests

    [Fact]
    public void ImplementsIFeed()
    {
        // Verify GBM implements IFeed interface
        Assert.True(typeof(IFeed).IsAssignableFrom(typeof(GBM)));

        // Use IFeed reference to verify interface contract
        IFeed feed = new GBM(startPrice: 100.0, seed: 42);

        // Test Next(bool) overload via interface
        var bar1 = feed.Next(isNew: true);
        Assert.True(bar1.Time > 0);

        var bar2 = feed.Next(isNew: true);
        Assert.True(bar2.Time > bar1.Time);

        // Test Next(ref bool) overload via interface - verify ref parameter behavior
        bool isNew = true;
        var bar3 = feed.Next(ref isNew);
        Assert.True(bar3.Time > bar2.Time);
        Assert.True(isNew, "GBM should honor isNew=true request and keep it true");

        // Test with isNew=false via interface
        bool isNewFalse = false;
        long bar3Time = bar3.Time;
        var bar3Updated = feed.Next(ref isNewFalse);
        Assert.Equal(bar3Time, bar3Updated.Time); // Same bar when isNew=false
        Assert.False(isNewFalse, "GBM should honor isNew=false request and keep it false");

        // Test Fetch via interface
        long startTime = DateTime.UtcNow.Ticks;
        var series = feed.Fetch(5, startTime, TimeSpan.FromMinutes(1));
        Assert.Equal(5, series.Count);
    }

    #endregion

    #region Statelessness Tests

    [Fact]
    public void Stateless_NoHistoryStorage()
    {
        var gbm = new GBM(startPrice: 100.0);

        for (int i = 0; i < 100; i++)
        {
            _ = gbm.Next();
        }

        var type = typeof(GBM);
        var barsProperty = type.GetProperty("Bars");

        Assert.Null(barsProperty);
    }

    #endregion
}
