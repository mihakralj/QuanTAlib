using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// StochRSI validation tests.
/// Cross-validates against Skender.Stock.Indicators.GetStochRsi,
/// TALib.NETCore StochRsi, OoplesFinance, and self-consistency checks.
/// </summary>
public sealed class StochrsiValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public StochrsiValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _data.Dispose();
            _disposed = true;
        }
    }

    private static TSeries GenerateCloseSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    // --- A) Streaming vs Batch self-consistency ---

    [Fact]
    public void Streaming_Matches_Batch()
    {
        var close = GenerateCloseSeries(300);
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        // Streaming
        var ind = new Stochrsi(rsiLen, stochLen, kSmooth, dSmooth);
        for (int i = 0; i < close.Count; i++)
        {
            ind.Update(new TValue(close.Times[i], close.Values[i]));
        }
        double streamK = ind.K;

        // Batch
        var batchResult = Stochrsi.Batch(close, rsiLen, stochLen, kSmooth, dSmooth);

        Assert.Equal(streamK, batchResult[^1].Value, 1e-10);
    }

    // --- B) Span matches TSeries ---

    [Fact]
    public void Span_Matches_TSeries()
    {
        var close = GenerateCloseSeries(200);
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var tsResult = Stochrsi.Batch(close, rsiLen, stochLen, kSmooth, dSmooth);

        double[] closeArr = close.Values.ToArray();
        var spanOut = new double[close.Count];
        Stochrsi.Batch(closeArr.AsSpan(), spanOut.AsSpan(), rsiLen, stochLen, kSmooth, dSmooth);

        for (int i = 0; i < close.Count; i++)
        {
            Assert.Equal(tsResult.Values[i], spanOut[i], 12);
        }
    }

    // --- C) Cross-validation with Skender ---

    [Fact]
    public void Skender_Batch_Validates()
    {
        // Skender GetStochRsi(rsiPeriod, stochPeriod, signalPeriod, smaPeriods)
        // signalPeriod = dSmooth, smaPeriods = kSmooth
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var qKD = new Stochrsi(rsiLen, stochLen, kSmooth, dSmooth).UpdateKD(_data.Data);

        var skResults = _data.SkenderQuotes.GetStochRsi(rsiLen, stochLen, dSmooth, kSmooth).ToList();

        // Skip warmup — compare converged values
        int warmup = rsiLen + stochLen + kSmooth + dSmooth;
        int totalCompared = 0;
        int mismatches = 0;

        for (int i = warmup; i < _data.Data.Count; i++)
        {
            double? skK = skResults[i].StochRsi;
            double? skD = skResults[i].Signal;

            if (skK.HasValue && skD.HasValue)
            {
                totalCompared++;
                double errK = Math.Abs(qKD.K.Values[i] - skK.Value);
                double errD = Math.Abs(qKD.D.Values[i] - skD.Value);

                if (errK > 1e-6 || errD > 1e-6)
                {
                    mismatches++;
                }
            }
        }

        Assert.True(totalCompared > 0, "No Skender results to compare");
        double mismatchRate = (double)mismatches / totalCompared;
        _output.WriteLine($"Skender batch: {totalCompared} compared, {mismatches} mismatches ({mismatchRate:P2})");
        Assert.True(mismatchRate < 0.05, $"Mismatch rate {mismatchRate:P2} exceeds 5% threshold ({mismatches}/{totalCompared})");
    }

    [Fact]
    public void Skender_Streaming_Validates()
    {
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var ind = new Stochrsi(rsiLen, stochLen, kSmooth, dSmooth);
        var qK = new List<double>();
        var qD = new List<double>();

        for (int i = 0; i < _data.Data.Count; i++)
        {
            ind.Update(new TValue(_data.Data.Times[i], _data.Data.Values[i]));
            qK.Add(ind.K);
            qD.Add(ind.D);
        }

        var skResults = _data.SkenderQuotes.GetStochRsi(rsiLen, stochLen, dSmooth, kSmooth).ToList();

        int warmup = rsiLen + stochLen + kSmooth + dSmooth;
        int totalCompared = 0;
        int mismatches = 0;

        for (int i = warmup; i < _data.Data.Count; i++)
        {
            double? skK = skResults[i].StochRsi;
            double? skD = skResults[i].Signal;

            if (skK.HasValue && skD.HasValue)
            {
                totalCompared++;
                double errK = Math.Abs(qK[i] - skK.Value);
                double errD = Math.Abs(qD[i] - skD.Value);

                if (errK > 1e-6 || errD > 1e-6)
                {
                    mismatches++;
                }
            }
        }

        Assert.True(totalCompared > 0, "No Skender results to compare");
        double mismatchRate = (double)mismatches / totalCompared;
        _output.WriteLine($"Skender streaming: {totalCompared} compared, {mismatches} mismatches ({mismatchRate:P2})");
        Assert.True(mismatchRate < 0.05, $"Mismatch rate {mismatchRate:P2} exceeds 5% ({mismatches}/{totalCompared})");
    }

    // --- D) Cross-validation with TALib ---

    [Fact]
    public void TALib_StochRsi_Validates()
    {
        // TALib StochRsi: timePeriod=rsiLen, fastK_Period=stochLen, fastD_Period=dSmooth
        // TALib does NOT smooth K (equivalent to kSmooth=1)
        const int rsiLen = 14;
        const int stochLen = 14;
        const int dSmooth = 3;

        double[] closeData = _data.RawData.ToArray();
        double[] taK = new double[closeData.Length];
        double[] taD = new double[closeData.Length];

        var retCode = TALib.Functions.StochRsi(closeData.AsSpan(), 0..^0,
            taK, taD, out var outRange, rsiLen, stochLen, dSmooth);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        var (offset, length) = outRange.GetOffsetAndLength(taK.Length);

        // Our indicator with kSmooth=1 to match TALib (no K smoothing)
        var ind = new Stochrsi(rsiLen, stochLen, kSmooth: 1, dSmooth);
        var qK = new List<double>();
        var qD = new List<double>();

        for (int i = 0; i < _data.Data.Count; i++)
        {
            ind.Update(new TValue(_data.Data.Times[i], _data.Data.Values[i]));
            qK.Add(ind.K);
            qD.Add(ind.D);
        }

        int matched = 0;
        int mismatches = 0;

        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            matched++;
            double errK = Math.Abs(qK[qi] - taK[j]);
            double errD = Math.Abs(qD[qi] - taD[j]);

            if (errK > 1e-6 || errD > 1e-6)
            {
                mismatches++;
            }
        }

        Assert.True(matched > 0, "No TALib results to compare");
        double mismatchRate = (double)mismatches / matched;
        _output.WriteLine($"TALib: {matched} compared, {mismatches} mismatches ({mismatchRate:P2})");
        Assert.True(mismatchRate < 0.05, $"TALib mismatch rate {mismatchRate:P2} exceeds 5% ({mismatches}/{matched})");
    }

    // --- E) Cross-validation with Ooples ---
    // Ooples CalculateStochasticRelativeStrengthIndex uses a fundamentally different
    // algorithm (EMA-based smoothing, different RSI seeding). Not directly comparable
    // to TradingView/Skender convention. Validated via Skender and TALib instead.

    [Fact]
    public void Ooples_StochRsi_Produces_Output()
    {
        var ooplesData = _data.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Close = (double)q.Close,
            High = (double)q.High,
            Low = (double)q.Low,
            Open = (double)q.Open,
            Volume = (double)q.Volume,
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateStochasticRelativeStrengthIndex();
        var oValues = oResult.OutputValues.Values.First();

        // Verify Ooples produces output (smoke test — algorithms differ)
        Assert.True(oValues.Count > 0, "Ooples should produce StochRSI output");

        int finiteCount = 0;
        for (int i = 50; i < oValues.Count; i++)
        {
            if (double.IsFinite(oValues[i]))
            {
                finiteCount++;
            }
        }
        _output.WriteLine($"Ooples StochRSI: {oValues.Count} values, {finiteCount} finite after warmup");
        Assert.True(finiteCount > 0, "Ooples should produce finite StochRSI values");
    }

    // --- F) Determinism ---

    [Fact]
    public void Deterministic_Across_Runs()
    {
        var close = GenerateCloseSeries(200, seed: 99);
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var r1 = Stochrsi.Batch(close, rsiLen, stochLen, kSmooth, dSmooth);
        var r2 = Stochrsi.Batch(close, rsiLen, stochLen, kSmooth, dSmooth);

        for (int i = 0; i < close.Count; i++)
        {
            Assert.Equal(r1.Values[i], r2.Values[i], 15);
        }
    }

    // --- G) Different parameters produce different results ---

    [Fact]
    public void Different_Periods_Produce_Different_Results()
    {
        var close = GenerateCloseSeries(200);

        var r1 = Stochrsi.Batch(close, rsiLength: 7, stochLength: 7, kSmooth: 3, dSmooth: 3);
        var r2 = Stochrsi.Batch(close, rsiLength: 21, stochLength: 21, kSmooth: 3, dSmooth: 3);

        bool anyDifferent = false;
        for (int i = 50; i < 200; i++)
        {
            if (Math.Abs(r1.Values[i] - r2.Values[i]) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    // --- H) Calculate returns hot indicator ---

    [Fact]
    public void Calculate_Returns_Hot_Indicator()
    {
        var close = GenerateCloseSeries(200);
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var (results, indicator) = Stochrsi.Calculate(close, rsiLen, stochLen, kSmooth, dSmooth);

        Assert.Equal(200, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.K));
        Assert.True(double.IsFinite(indicator.D));
    }

    // --- I) Range validation (values should be 0-100) ---

    [Fact]
    public void Values_Within_0_100_Range()
    {
        var close = GenerateCloseSeries(500);
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var kd = new Stochrsi(rsiLen, stochLen, kSmooth, dSmooth).UpdateKD(close);

        int warmup = rsiLen + stochLen + kSmooth + dSmooth;
        for (int i = warmup; i < close.Count; i++)
        {
            double k = kd.K.Values[i];
            double d = kd.D.Values[i];

            Assert.True(k >= -0.01 && k <= 100.01,
                $"K value {k} out of range at index {i}");
            Assert.True(d >= -0.01 && d <= 100.01,
                $"D value {d} out of range at index {i}");
        }
    }

    // --- J) Skender span validation ---

    [Fact]
    public void Skender_Span_Validates()
    {
        const int rsiLen = 14;
        const int stochLen = 14;
        const int kSmooth = 3;
        const int dSmooth = 3;

        double[] closeData = _data.RawData.ToArray();
        var spanOut = new double[closeData.Length];
        Stochrsi.Batch(closeData.AsSpan(), spanOut.AsSpan(), rsiLen, stochLen, kSmooth, dSmooth);

        var skResults = _data.SkenderQuotes.GetStochRsi(rsiLen, stochLen, dSmooth, kSmooth).ToList();

        int warmup = rsiLen + stochLen + kSmooth + dSmooth;
        int totalCompared = 0;
        int mismatches = 0;

        for (int i = warmup; i < closeData.Length; i++)
        {
            double? skK = skResults[i].StochRsi;
            if (skK.HasValue)
            {
                totalCompared++;
                double err = Math.Abs(spanOut[i] - skK.Value);
                if (err > 1e-6)
                {
                    mismatches++;
                }
            }
        }

        Assert.True(totalCompared > 0, "No Skender results to compare");
        double mismatchRate = (double)mismatches / totalCompared;
        _output.WriteLine($"Skender span: {totalCompared} compared, {mismatches} mismatches ({mismatchRate:P2})");
        Assert.True(mismatchRate < 0.05, $"Skender span mismatch rate {mismatchRate:P2} exceeds 5% ({mismatches}/{totalCompared})");
    }

    [Fact]
    public void Stochrsi_Correction_Recomputes()
    {
        var ind = new Stochrsi();
        var t0 = DateTime.MinValue;

        // Build state well past warmup (WarmupPeriod ≈ 31)
        for (int i = 0; i < 50; i++)
        {
            ind.Update(new TValue(t0.AddSeconds(i), 100.0 + (i * 0.5)));
        }

        // Anchor bar
        var anchorTime = t0.AddSeconds(50);
        const double anchorPrice = 125.0;
        ind.Update(new TValue(anchorTime, anchorPrice), isNew: true);
        double anchorK = ind.K;
        double anchorD = ind.D;

        // Use large downward spike (÷10) to move StochRSI away from ceiling
        ind.Update(new TValue(anchorTime, anchorPrice / 10), isNew: false);
        Assert.NotEqual(anchorK, ind.K);

        // Correction back to original — both outputs must restore exactly
        ind.Update(new TValue(anchorTime, anchorPrice), isNew: false);
        Assert.Equal(anchorK, ind.K, 1e-9);
        Assert.Equal(anchorD, ind.D, 1e-9);
    }
}
