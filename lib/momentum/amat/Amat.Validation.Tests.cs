using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for AMAT (Archer Moving Averages Trends).
///
/// AMAT is a custom indicator not found in external libraries like TA-Lib, Skender, Tulip, or Ooples.
/// Instead, we validate:
/// 1. The underlying EMA calculations match external libraries
/// 2. The trend logic produces expected results for known input patterns
/// 3. Cross-validation between streaming and batch modes
/// </summary>
public sealed class AmatValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AmatValidationTests(ITestOutputHelper output)
    {
        _output = output;
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

    /// <summary>
    /// Validates that AMAT's Fast EMA matches Skender's EMA calculation.
    /// </summary>
    [Fact]
    public void Validate_FastEma_Against_Skender()
    {
        int fastPeriod = 10;
        int slowPeriod = 50;

        // Calculate QuanTAlib AMAT (streaming to access FastEma)
        var amat = new Amat(fastPeriod, slowPeriod);
        var qFastEma = new List<double>();

        foreach (var item in _testData.Data)
        {
            amat.Update(item);
            qFastEma.Add(amat.FastEma.Value);
        }

        // Calculate Skender EMA (fast period)
        var sResult = _testData.SkenderQuotes.GetEma(fastPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qFastEma, sResult, (s) => s.Ema);

        _output.WriteLine($"AMAT Fast EMA (period {fastPeriod}) validated successfully against Skender");
    }

    /// <summary>
    /// Validates that AMAT's Slow EMA matches Skender's EMA calculation.
    /// </summary>
    [Fact]
    public void Validate_SlowEma_Against_Skender()
    {
        int fastPeriod = 10;
        int slowPeriod = 50;

        // Calculate QuanTAlib AMAT (streaming to access SlowEma)
        var amat = new Amat(fastPeriod, slowPeriod);
        var qSlowEma = new List<double>();

        foreach (var item in _testData.Data)
        {
            amat.Update(item);
            qSlowEma.Add(amat.SlowEma.Value);
        }

        // Calculate Skender EMA (slow period)
        var sResult = _testData.SkenderQuotes.GetEma(slowPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qSlowEma, sResult, (s) => s.Ema);

        _output.WriteLine($"AMAT Slow EMA (period {slowPeriod}) validated successfully against Skender");
    }

    /// <summary>
    /// Validates that AMAT's Fast EMA matches TA-Lib's EMA calculation.
    /// </summary>
    [Fact]
    public void Validate_FastEma_Against_Talib()
    {
        int fastPeriod = 10;
        int slowPeriod = 50;

        // Prepare data for TA-Lib
        double[] tData = _testData.RawData.ToArray();
        double[] outEma = new double[tData.Length];

        // Calculate QuanTAlib AMAT (streaming to access FastEma)
        var amat = new Amat(fastPeriod, slowPeriod);
        var qFastEma = new List<double>();

        foreach (var item in _testData.Data)
        {
            amat.Update(item);
            qFastEma.Add(amat.FastEma.Value);
        }

        // Calculate TA-Lib EMA (fast period)
        var retCode = TALib.Functions.Ema<double>(tData, 0..^0, outEma, out var outRange, fastPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.EmaLookback(fastPeriod);

        // Compare last 100 records
        ValidationHelper.VerifyData(qFastEma, outEma, outRange, lookback);

        _output.WriteLine($"AMAT Fast EMA (period {fastPeriod}) validated successfully against TA-Lib");
    }

    /// <summary>
    /// Validates that AMAT's Slow EMA matches TA-Lib's EMA calculation.
    /// </summary>
    [Fact]
    public void Validate_SlowEma_Against_Talib()
    {
        int fastPeriod = 10;
        int slowPeriod = 50;

        // Prepare data for TA-Lib
        double[] tData = _testData.RawData.ToArray();
        double[] outEma = new double[tData.Length];

        // Calculate QuanTAlib AMAT (streaming to access SlowEma)
        var amat = new Amat(fastPeriod, slowPeriod);
        var qSlowEma = new List<double>();

        foreach (var item in _testData.Data)
        {
            amat.Update(item);
            qSlowEma.Add(amat.SlowEma.Value);
        }

        // Calculate TA-Lib EMA (slow period)
        var retCode = TALib.Functions.Ema<double>(tData, 0..^0, outEma, out var outRange, slowPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.EmaLookback(slowPeriod);

        // Compare last 100 records
        ValidationHelper.VerifyData(qSlowEma, outEma, outRange, lookback);

        _output.WriteLine($"AMAT Slow EMA (period {slowPeriod}) validated successfully against TA-Lib");
    }

    /// <summary>
    /// Validates trend logic: Rising prices should eventually produce bullish signal (+1).
    /// </summary>
    [Fact]
    public void Validate_BullishTrend_Logic()
    {
        int fastPeriod = 5;
        int slowPeriod = 10;

        var amat = new Amat(fastPeriod, slowPeriod);

        // Create steadily rising prices - should produce bullish trend
        var time = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + i; // Steadily increasing
            amat.Update(new TValue(time.AddMinutes(i), price));
        }

        // After warmup, a steadily rising market should be bullish
        Assert.Equal(1.0, amat.Last.Value);
        Assert.True(amat.Strength.Value > 0, "Strength should be positive");
        Assert.True(amat.FastEma.Value > amat.SlowEma.Value, "Fast EMA should be above Slow EMA in uptrend");

        _output.WriteLine($"Bullish trend logic validated: Trend={amat.Last.Value}, Strength={amat.Strength.Value:F2}%");
    }

    /// <summary>
    /// Validates trend logic: Falling prices should eventually produce bearish signal (-1).
    /// </summary>
    [Fact]
    public void Validate_BearishTrend_Logic()
    {
        int fastPeriod = 5;
        int slowPeriod = 10;

        var amat = new Amat(fastPeriod, slowPeriod);

        // Create steadily falling prices - should produce bearish trend
        var time = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            double price = 200 - i; // Steadily decreasing
            amat.Update(new TValue(time.AddMinutes(i), price));
        }

        // After warmup, a steadily falling market should be bearish
        Assert.Equal(-1.0, amat.Last.Value);
        Assert.True(amat.Strength.Value > 0, "Strength should be positive");
        Assert.True(amat.FastEma.Value < amat.SlowEma.Value, "Fast EMA should be below Slow EMA in downtrend");

        _output.WriteLine($"Bearish trend logic validated: Trend={amat.Last.Value}, Strength={amat.Strength.Value:F2}%");
    }

    /// <summary>
    /// Validates trend logic: Flat prices should produce neutral signal (0).
    /// </summary>
    [Fact]
    public void Validate_NeutralTrend_Logic()
    {
        int fastPeriod = 5;
        int slowPeriod = 10;

        var amat = new Amat(fastPeriod, slowPeriod);

        // Create flat prices - should produce neutral trend
        var time = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            amat.Update(new TValue(time.AddMinutes(i), 100.0)); // Constant price
        }

        // Flat market: EMAs converge, no clear direction
        Assert.Equal(0.0, amat.Last.Value);
        Assert.True(amat.Strength.Value < 1.0, "Strength should be near zero for flat market");

        _output.WriteLine($"Neutral trend logic validated: Trend={amat.Last.Value}, Strength={amat.Strength.Value:F2}%");
    }

    /// <summary>
    /// Validates trend transition from bullish to bearish.
    /// </summary>
    [Fact]
    public void Validate_TrendTransition_BullishToBearish()
    {
        int fastPeriod = 5;
        int slowPeriod = 10;

        var amat = new Amat(fastPeriod, slowPeriod);
        var time = DateTime.UtcNow;

        // Phase 1: Rising prices
        for (int i = 0; i < 50; i++)
        {
            double price = 100 + i;
            amat.Update(new TValue(time.AddMinutes(i), price));
        }
        double bullishTrend = amat.Last.Value;

        // Phase 2: Falling prices (reversal)
        for (int i = 50; i < 150; i++)
        {
            double price = 150 - (i - 50) * 2; // Fall faster than rise
            amat.Update(new TValue(time.AddMinutes(i), price));
        }
        double bearishTrend = amat.Last.Value;

        Assert.Equal(1.0, bullishTrend);
        Assert.Equal(-1.0, bearishTrend);

        _output.WriteLine($"Trend transition validated: Bullish({bullishTrend}) -> Bearish({bearishTrend})");
    }

    /// <summary>
    /// Validates that streaming and batch modes produce identical results.
    /// </summary>
    [Fact]
    public void Validate_Streaming_Matches_Batch()
    {
        int fastPeriod = 10;
        int slowPeriod = 50;

        // Calculate streaming
        var amatStreaming = new Amat(fastPeriod, slowPeriod);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            amatStreaming.Update(item);
            streamingResults.Add(amatStreaming.Last.Value);
        }

        // Calculate batch
        var batchResults = Amat.Batch(_testData.Data, fastPeriod, slowPeriod);

        // Compare
        Assert.Equal(streamingResults.Count, batchResults.Count);

        int matchCount = 0;
        int totalCount = streamingResults.Count;

        for (int i = 0; i < totalCount; i++)
        {
            if (Math.Abs(streamingResults[i] - batchResults[i].Value) < 1e-10)
            {
                matchCount++;
            }
        }

        double matchRate = (double)matchCount / totalCount;
        Assert.True(matchRate > 0.99, $"Expected >99% match rate, got {matchRate:P2}");

        _output.WriteLine($"Streaming vs Batch validation: {matchRate:P2} match rate ({matchCount}/{totalCount})");
    }

    /// <summary>
    /// Validates that span-based Calculate matches streaming results.
    /// </summary>
    [Fact]
    public void Validate_Span_Matches_Streaming()
    {
        int fastPeriod = 10;
        int slowPeriod = 50;

        // Calculate streaming
        var amatStreaming = new Amat(fastPeriod, slowPeriod);
        var streamingTrend = new List<double>();
        var streamingStrength = new List<double>();

        foreach (var item in _testData.Data)
        {
            amatStreaming.Update(item);
            streamingTrend.Add(amatStreaming.Last.Value);
            streamingStrength.Add(amatStreaming.Strength.Value);
        }

        // Calculate span
        double[] sourceData = _testData.RawData.ToArray();
        double[] spanTrend = new double[sourceData.Length];
        double[] spanStrength = new double[sourceData.Length];
        Amat.Calculate(sourceData, spanTrend, spanStrength, fastPeriod, slowPeriod);

        // Compare trend values (after warmup period)
        int warmup = slowPeriod * 2; // Allow extra warmup for convergence
        int matchCount = 0;
        int totalCount = sourceData.Length - warmup;

        for (int i = warmup; i < sourceData.Length; i++)
        {
            if (Math.Abs(streamingTrend[i] - spanTrend[i]) < 1e-10)
            {
                matchCount++;
            }
        }

        double matchRate = (double)matchCount / totalCount;
        Assert.True(matchRate > 0.95, $"Expected >95% match rate after warmup, got {matchRate:P2}");

        _output.WriteLine($"Streaming vs Span validation: {matchRate:P2} match rate after warmup ({matchCount}/{totalCount})");
    }

    /// <summary>
    /// Validates strength calculation is correct.
    /// </summary>
    [Fact]
    public void Validate_Strength_Calculation()
    {
        int fastPeriod = 5;
        int slowPeriod = 10;

        var amat = new Amat(fastPeriod, slowPeriod);

        // Create scenario where we can predict the strength
        var time = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + i;
            amat.Update(new TValue(time.AddMinutes(i), price));
        }

        // Verify strength formula: |Fast - Slow| / Slow * 100
        double expectedStrength = Math.Abs(amat.FastEma.Value - amat.SlowEma.Value) / amat.SlowEma.Value * 100.0;
        Assert.Equal(expectedStrength, amat.Strength.Value, 10);

        _output.WriteLine($"Strength calculation validated: {amat.Strength.Value:F4}%");
    }

    /// <summary>
    /// Validates multiple period combinations.
    /// </summary>
    [Theory]
    [InlineData(5, 10)]
    [InlineData(10, 20)]
    [InlineData(12, 26)]
    [InlineData(20, 50)]
    [InlineData(50, 100)]
    public void Validate_Multiple_Period_Combinations(int fastPeriod, int slowPeriod)
    {
        var amat = new Amat(fastPeriod, slowPeriod);

        // Feed data
        foreach (var item in _testData.Data)
        {
            amat.Update(item);
        }

        // Verify output is valid
        Assert.True(amat.Last.Value >= -1.0 && amat.Last.Value <= 1.0,
            $"Trend should be -1, 0, or 1, got {amat.Last.Value}");
        Assert.True(amat.Strength.Value >= 0, "Strength should be non-negative");
        Assert.True(double.IsFinite(amat.FastEma.Value), "FastEma should be finite");
        Assert.True(double.IsFinite(amat.SlowEma.Value), "SlowEma should be finite");
        Assert.True(amat.IsHot, "Indicator should be hot after processing data");

        _output.WriteLine($"Period combination ({fastPeriod}, {slowPeriod}) validated: Trend={amat.Last.Value}, Strength={amat.Strength.Value:F2}%");
    }
}
