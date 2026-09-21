using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
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
            double price = 100.0 + (i * 3.0); // Strong linear uptrend
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
            double price = 100.0 + (5.0 * Math.Sin(2.0 * Math.PI * i / 3.0));
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

    // ── Cross-library: OoplesFinance ──────────────────────────────────────────
    [Fact]
    public void Chop_MatchesOoples_Structural()
    {
        const int period = 14;
        var ooplesData = _data.Bars.Select(static b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateChoppinessIndex(length: period);
        var oValues = oResult.OutputValues.Values.First();

        var chop = new Chop(period);
        var qValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            qValues.Add(chop.Update(bar).Value);
        }

        Assert.True(oValues.Count > 0, "Ooples Chop must produce output");
        int finiteCount = 0;
        for (int i = period; i < Math.Min(oValues.Count, qValues.Count); i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qValues[i]))
            {
                finiteCount++;
            }
        }
        Assert.True(finiteCount > 100, $"Expected >100 finite Chop pairs, got {finiteCount}");
    }
}
