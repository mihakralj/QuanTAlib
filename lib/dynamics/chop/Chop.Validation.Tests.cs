using Skender.Stock.Indicators;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CHOP (Choppiness Index) indicator.
/// Validates against Skender.Stock.Indicators GetChop implementation
/// and mathematical properties of the ATR-based range normalization.
/// </summary>
public sealed class ChopValidationTests : IDisposable
{
    private readonly ValidationTestData _data;
    private bool _disposed;

    public ChopValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _data?.Dispose();
        }
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        var chop = new Chop(14);
        var qResults = new List<double>();

        foreach (var bar in _data.Bars)
        {
            qResults.Add(chop.Update(bar).Value);
        }

        var skenderResults = _data.SkenderQuotes.GetChop(14).ToList();

        ValidationHelper.VerifyData(qResults, skenderResults, s => s.Chop, tolerance: ValidationHelper.SkenderTolerance);
    }

    [Fact]
    public void Validation_OutputRange_ZeroTo100()
    {
        // CHOP is bounded between 0 and 100 (uses log10 normalization)
        var chop = new Chop(14);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            chop.Update(bar);
            if (chop.IsHot)
            {
                double val = chop.Last.Value;
                Assert.True(val >= 0.0 && val <= 100.0,
                    $"CHOP value {val} is outside expected range [0, 100]");
            }
        }
    }

    [Fact]
    public void Validation_TrendingMarket_LowChop()
    {
        // Strong directional movement should produce low CHOP (below 50)
        var chop = new Chop(14);

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + i * 3.0; // Strong linear uptrend
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000);
            chop.Update(bar);
        }

        if (chop.IsHot)
        {
            Assert.True(chop.Last.Value < 50.0,
                $"Trending market should produce low CHOP (<50), got {chop.Last.Value}");
        }
    }

    [Fact]
    public void Validation_ChoppyMarket_HighChop()
    {
        // Choppy (range-bound) market should produce high CHOP (above 50)
        var chop = new Chop(14);

        for (int i = 0; i < 100; i++)
        {
            // Oscillating price with wide range but no trend
            double price = 100.0 + 5.0 * Math.Sin(2.0 * Math.PI * i / 3.0);
            double high = price + 3.0;
            double low = price - 3.0;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, high, low, price, 1000);
            chop.Update(bar);
        }

        if (chop.IsHot)
        {
            Assert.True(chop.Last.Value > 50.0,
                $"Choppy market should produce high CHOP (>50), got {chop.Last.Value}");
        }
    }

    [Fact]
    public void Validation_FiniteOutputs_AfterWarmup()
    {
        var chop = new Chop(14);

        var gbm = new GBM(seed: 99);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            chop.Update(bar);
            if (chop.IsHot)
            {
                Assert.True(double.IsFinite(chop.Last.Value),
                    $"CHOP produced non-finite value after warmup: {chop.Last.Value}");
            }
        }
    }
}
