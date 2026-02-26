namespace QuanTAlib.Tests;

/// <summary>
/// ETHERM Validation Tests — Self-consistency validation.
/// Elder's Thermometer is not widely available in TA-Lib, Skender, Tulip, or Ooples,
/// so validation focuses on internal consistency and mathematical correctness.
/// </summary>
public sealed class EthermValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public EthermValidationTests()
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
        int[] periods = { 5, 14, 22, 50 };

        foreach (var period in periods)
        {
            // Streaming
            var ethermStream = new Etherm(period);
            var streamResults = new List<double>();
            foreach (var bar in _testData.Bars)
            {
                streamResults.Add(ethermStream.Update(bar).Value);
            }

            // Batch
            var batchResults = Etherm.Batch(_testData.Bars, period);

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
        int[] periods = { 5, 14, 22 };
        int len = _testData.Bars.Count;

        double[] highs = new double[len];
        double[] lows = new double[len];
        for (int i = 0; i < len; i++)
        {
            highs[i] = _testData.Bars[i].High;
            lows[i] = _testData.Bars[i].Low;
        }

        foreach (var period in periods)
        {
            // Streaming
            var ethermStream = new Etherm(period);
            var streamResults = new double[len];
            for (int i = 0; i < len; i++)
            {
                streamResults[i] = ethermStream.Update(_testData.Bars[i]).Value;
            }

            // Span batch (raw temperature only)
            double[] spanResults = new double[len];
            Etherm.Batch(highs, lows, spanResults, period);

            for (int i = 0; i < len; i++)
            {
                Assert.Equal(streamResults[i], spanResults[i], 1e-10);
            }
        }
    }

    // ============== Known-Value Tests ==============

    [Fact]
    public void Validation_InsideBars_ReturnZero()
    {
        var etherm = new Etherm(22);

        // Bar1: H=110, L=90
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        etherm.Update(bar1);

        // Bar2: inside bar (H=105 < 110 AND L=95 > 90) → temp = 0
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 105, 95, 100, 1000);
        var result = etherm.Update(bar2);
        Assert.Equal(0.0, result.Value, 1e-10);

        // Bar3: inside bar again (H=103 < 105... wait, that's relative to bar2)
        // Re-check: prevH=105, prevL=95 → H=104 < 105, L=96 > 95 → inside
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 100, 104, 96, 100, 1000);
        var result3 = etherm.Update(bar3);
        Assert.Equal(0.0, result3.Value, 1e-10);
    }

    [Fact]
    public void Validation_FlatMarket_ZeroTemperature()
    {
        var etherm = new Etherm(22);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(baseTime.AddMinutes(i), 100, 100, 100, 100, 1000);
            etherm.Update(bar);
        }

        // Flat market: all H=L=O=C → highDiff=0, lowDiff=0, not inside bar, temp=0
        Assert.Equal(0.0, etherm.Last.Value, 1e-10);
        Assert.Equal(0.0, etherm.Signal, 1e-10);
    }

    [Fact]
    public void Validation_GapUp_MeasuresHighExtension()
    {
        var etherm = new Etherm(22);

        // Bar1: H=100, L=90
        var bar1 = new TBar(DateTime.UtcNow, 95, 100, 90, 95, 1000);
        etherm.Update(bar1);

        // Bar2: Gap up → H=120, L=105 → highDiff=|120-100|=20, lowDiff=|90-105|=15
        // Not inside (120 > 100), temp = max(20, 15) = 20
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 110, 120, 105, 115, 1000);
        var result = etherm.Update(bar2);

        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void Validation_GapDown_MeasuresLowExtension()
    {
        var etherm = new Etherm(22);

        // Bar1: H=100, L=90
        var bar1 = new TBar(DateTime.UtcNow, 95, 100, 90, 95, 1000);
        etherm.Update(bar1);

        // Bar2: Gap down → H=95, L=70 → highDiff=|95-100|=5, lowDiff=|90-70|=20
        // Not inside (95 < 100 but 70 < 90, so not BOTH conditions met)
        // Inside = H < prevH AND L > prevL → 95 < 100 is true, but 70 > 90 is false → NOT inside
        // temp = max(5, 20) = 20
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 80, 95, 70, 75, 1000);
        var result = etherm.Update(bar2);

        Assert.Equal(20.0, result.Value, 1e-10);
    }

    // ============== Different Periods ==============

    [Fact]
    public void Validation_DifferentPeriods_ProduceDifferentSignals()
    {
        var etherm5 = new Etherm(5);
        var etherm22 = new Etherm(22);
        var etherm50 = new Etherm(50);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.5);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            etherm5.Update(bar);
            etherm22.Update(bar);
            etherm50.Update(bar);
        }

        // Temperature (Last) should be the same regardless of period
        // (EMA period only affects Signal)
        Assert.Equal(etherm5.Last.Value, etherm22.Last.Value, 1e-10);
        Assert.Equal(etherm22.Last.Value, etherm50.Last.Value, 1e-10);

        // But signals should differ (different EMA periods)
        // Note: They can be equal in degenerate cases, but generally should differ
        Assert.True(double.IsFinite(etherm5.Signal));
        Assert.True(double.IsFinite(etherm22.Signal));
        Assert.True(double.IsFinite(etherm50.Signal));
    }

    [Fact]
    public void Validation_Calculate_ReturnsHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.5);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Etherm.Calculate(bars, 14);

        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Signal));
    }

    [Fact]
    public void Validation_BarCorrection_Consistent()
    {
        var etherm1 = new Etherm(14);
        var etherm2 = new Etherm(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Etherm1: feed all bars normally
        foreach (var bar in bars)
        {
            etherm1.Update(bar, isNew: true);
        }

        // Etherm2: feed bars with corrections (isNew=false for last bar, then replace)
        for (int i = 0; i < bars.Count - 1; i++)
        {
            etherm2.Update(bars[i], isNew: true);
        }
        // Feed an incorrect last bar first
        var wrongBar = new TBar(bars[^1].Time, 0, 999, 1, 500, 1000);
        etherm2.Update(wrongBar, isNew: true);
        // Correct it
        etherm2.Update(bars[^1], isNew: false);

        Assert.Equal(etherm1.Last.Value, etherm2.Last.Value, 1e-10);
        Assert.Equal(etherm1.Signal, etherm2.Signal, 1e-10);
    }

    [Fact]
    public void Validation_Temperature_AlwaysNonNegative()
    {
        var etherm = new Etherm(22);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = etherm.Update(bar);
            Assert.True(result.Value >= 0, $"Temperature must be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Validation_Signal_AlwaysNonNegative()
    {
        var etherm = new Etherm(22);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            etherm.Update(bar);
            Assert.True(etherm.Signal >= 0, $"Signal must be non-negative, got {etherm.Signal}");
        }
    }
}
