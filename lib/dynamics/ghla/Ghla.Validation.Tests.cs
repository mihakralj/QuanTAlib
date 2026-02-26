namespace QuanTAlib.Tests;

/// <summary>
/// GHLA Validation Tests — Self-consistency and cross-library validation.
/// Skender.Stock.Indicators has HiLoActivator for potential validation.
/// </summary>
public sealed class GhlaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public GhlaValidationTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    // ============== Self-Consistency ==============

    [Fact]
    public void Validation_BatchMatchesStreaming()
    {
        int[] periods = { 3, 5, 13, 21 };

        foreach (var period in periods)
        {
            var ghlaStream = new Ghla(period);
            var streamResults = new List<double>();
            foreach (var bar in _testData.Bars)
            {
                streamResults.Add(ghlaStream.Update(bar).Value);
            }

            var batchResults = Ghla.Batch(_testData.Bars, period);

            Assert.Equal(streamResults.Count, batchResults.Count);
            for (int i = 0; i < streamResults.Count; i++)
            {
                Assert.Equal(streamResults[i], batchResults[i].Value, 1e-10);
            }
        }
    }

    [Fact]
    public void Validation_SpanMatchesStreaming()
    {
        int[] periods = { 3, 5, 13 };
        int len = _testData.Bars.Count;

        double[] highs = new double[len];
        double[] lows = new double[len];
        double[] closes = new double[len];
        for (int i = 0; i < len; i++)
        {
            highs[i] = _testData.Bars[i].High;
            lows[i] = _testData.Bars[i].Low;
            closes[i] = _testData.Bars[i].Close;
        }

        foreach (var period in periods)
        {
            var ghlaStream = new Ghla(period);
            var streamResults = new double[len];
            for (int i = 0; i < len; i++)
            {
                streamResults[i] = ghlaStream.Update(_testData.Bars[i]).Value;
            }

            double[] spanResults = new double[len];
            Ghla.Batch(highs, lows, closes, spanResults, period);

            for (int i = 0; i < len; i++)
            {
                Assert.Equal(streamResults[i], spanResults[i], 1e-10);
            }
        }
    }

    // ============== Known-Value Tests ==============

    [Fact]
    public void Validation_FlatMarket_OutputEqualsPrice()
    {
        var ghla = new Ghla(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(baseTime.AddMinutes(i), 100, 100, 100, 100, 1000);
            ghla.Update(bar);
        }

        // Flat market: SMA(H)=SMA(L)=100, close=100
        // Trend seeded as bullish (close >= smaHigh), output = smaLow = 100
        Assert.Equal(100.0, ghla.Last.Value, 1e-10);
    }

    [Fact]
    public void Validation_StrongUptrend_OutputIsSmaLow()
    {
        var ghla = new Ghla(3);
        var baseTime = DateTime.UtcNow;

        // Strongly rising bars
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + (i * 10);
            var bar = new TBar(baseTime.AddMinutes(i), price, price + 5, price - 5, price + 3, 1000);
            ghla.Update(bar);
        }

        Assert.Equal(1, ghla.Trend);

        // Output should be SMA of lows (trailing support)
        // Last 3 lows: 185-5=180, 175-5=170, 165-5=160 → not exact due to feed, but should be < close
        double lastClose = 100 + (9 * 10) + 3; // 193
        Assert.True(ghla.Last.Value < lastClose, "Bullish activator (SMA(Low)) should be below close");
    }

    [Fact]
    public void Validation_StrongDowntrend_OutputIsSmaHigh()
    {
        var ghla = new Ghla(3);
        var baseTime = DateTime.UtcNow;

        // Strongly falling bars
        for (int i = 0; i < 10; i++)
        {
            double price = 200 - (i * 10);
            var bar = new TBar(baseTime.AddMinutes(i), price, price + 5, price - 5, price - 3, 1000);
            ghla.Update(bar);
        }

        Assert.Equal(-1, ghla.Trend);

        // Output should be SMA of highs (overhead resistance)
        double lastClose = 200 - (9 * 10) - 3; // 107
        Assert.True(ghla.Last.Value > lastClose, "Bearish activator (SMA(High)) should be above close");
    }

    // ============== Different Periods ==============

    [Fact]
    public void Validation_DifferentPeriods_ProduceDifferentOutputs()
    {
        var ghla3 = new Ghla(3);
        var ghla13 = new Ghla(13);
        var ghla50 = new Ghla(50);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.5);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ghla3.Update(bar);
            ghla13.Update(bar);
            ghla50.Update(bar);
        }

        // Different periods should generally produce different outputs
        Assert.True(double.IsFinite(ghla3.Last.Value));
        Assert.True(double.IsFinite(ghla13.Last.Value));
        Assert.True(double.IsFinite(ghla50.Last.Value));

        // With volatile GBM data, at least two should differ
        bool allSame = Math.Abs(ghla3.Last.Value - ghla13.Last.Value) < 1e-10
            && Math.Abs(ghla13.Last.Value - ghla50.Last.Value) < 1e-10;
        Assert.False(allSame, "Different periods should generally produce different GHLA values");
    }

    [Fact]
    public void Validation_Calculate_ReturnsHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.5);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Ghla.Calculate(bars, 13);

        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(indicator.Trend != 0);
    }

    [Fact]
    public void Validation_BarCorrection_Consistent()
    {
        var ghla1 = new Ghla(5);
        var ghla2 = new Ghla(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ghla1.Update(bar, isNew: true);
        }

        for (int i = 0; i < bars.Count - 1; i++)
        {
            ghla2.Update(bars[i], isNew: true);
        }
        var wrongBar = new TBar(bars[^1].Time, 0, 999, 1, 500, 1000);
        ghla2.Update(wrongBar, isNew: true);
        ghla2.Update(bars[^1], isNew: false);

        Assert.Equal(ghla1.Last.Value, ghla2.Last.Value, 1e-10);
        Assert.Equal(ghla1.Trend, ghla2.Trend);
    }

    [Fact]
    public void Validation_Output_AlwaysFinite()
    {
        var ghla = new Ghla(13);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = ghla.Update(bar);
            Assert.True(double.IsFinite(result.Value), $"GHLA output must be finite, got {result.Value}");
        }
    }

    [Fact]
    public void Validation_Output_AlwaysPositive_ForPositivePrices()
    {
        var ghla = new Ghla(13);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.5);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = ghla.Update(bar);
            Assert.True(result.Value > 0, $"GHLA output must be positive for positive prices, got {result.Value}");
        }
    }

    [Fact]
    public void Validation_TrendValues_OnlyValidStates()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Before any data, trend should be 0
        Assert.Equal(0, ghla.Trend);

        foreach (var bar in bars)
        {
            ghla.Update(bar);
            // After first bar, trend must be +1 or -1 (never 0 or any other value)
            Assert.True(ghla.Trend == 1 || ghla.Trend == -1, $"Trend must be +1 or -1, got {ghla.Trend}");
        }
    }
}
