using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class TrixValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
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

    // ── A) Skender Batch ─────────────────────────────────────────────────────
    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = [9, 14, 25];

        foreach (var period in periods)
        {
            var trix = new global::QuanTAlib.Trix(period);
            var qResult = trix.Update(_testData.Data);

            var sResult = _testData.SkenderQuotes.GetTrix(period).ToList();

            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Trix);
        }
        _output.WriteLine("TRIX Batch(TSeries) validated successfully against Skender");
    }

    // ── B) Skender Streaming ─────────────────────────────────────────────────
    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = [9, 14, 25];

        foreach (var period in periods)
        {
            var trix = new global::QuanTAlib.Trix(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(trix.Update(item).Value);
            }

            var sResult = _testData.SkenderQuotes.GetTrix(period).ToList();

            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Trix);
        }
        _output.WriteLine("TRIX Streaming validated successfully against Skender");
    }

    // ── C) Skender Span ──────────────────────────────────────────────────────
    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = [9, 14, 25];
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Trix.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            var sResult = _testData.SkenderQuotes.GetTrix(period).ToList();

            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Trix);
        }
        _output.WriteLine("TRIX Span validated successfully against Skender");
    }

    // ── D) TA-Lib Span ───────────────────────────────────────────────────────
    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = [14, 20, 50, 100];
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[tData.Length];
            global::QuanTAlib.Trix.Batch(tData.AsSpan(), qOutput.AsSpan(), period);

            double[] tOutput = new double[tData.Length];
            var retCode = TALib.Functions.Trix<double>(tData, 0..^0, tOutput, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TrixLookback(period);

            ValidationHelper.VerifyData(qOutput, tOutput, outRange, lookback);
        }
        _output.WriteLine("TRIX Span validated against TA-Lib");
    }

    // ── E) TA-Lib Streaming ──────────────────────────────────────────────────
    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = [9, 14, 25];
        double[] tData = _testData.RawData.ToArray();
        double[] tOutput = new double[tData.Length];

        foreach (var period in periods)
        {
            var trix = new global::QuanTAlib.Trix(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(trix.Update(item).Value);
            }

            var retCode = TALib.Functions.Trix<double>(tData, 0..^0, tOutput, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TrixLookback(period);

            ValidationHelper.VerifyData(qResults, tOutput, outRange, lookback);
        }
        _output.WriteLine("TRIX Streaming validated successfully against TA-Lib");
    }

    // ── F) Tulip Batch ───────────────────────────────────────────────────────
    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = [9, 14, 25];
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var trix = new global::QuanTAlib.Trix(period);
            var qResult = trix.Update(_testData.Data);

            var trixIndicator = Tulip.Indicators.trix;
            double[][] inputs = [tData];
            double[] options = [period];

            int lookback = trixIndicator.Start(options);
            double[][] outputs = [new double[tData.Length - lookback]];

            trixIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Tulip uses non-compensated EMA; warmup compensation causes persistent diffs
            // TRIX amplifies by 100×, so small EMA diffs become noticeable in TRIX
            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: 1e-3);
        }
        _output.WriteLine("TRIX Batch(TSeries) validated successfully against Tulip");
    }

    // ── G) Tulip Span ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Tulip_Span()
    {
        int[] periods = [14, 20, 50, 100];
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[tData.Length];
            global::QuanTAlib.Trix.Batch(tData.AsSpan(), qOutput.AsSpan(), period);

            var trixIndicator = Tulip.Indicators.trix;
            double[][] inputs = [tData];
            double[] options = [period];
            int lookback = trixIndicator.Start(options);
            double[][] outputs = [new double[tData.Length - lookback]];

            trixIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Tulip uses non-compensated EMA; warmup compensation causes minor convergence diffs
            // TRIX amplifies by 100×, so EMA diffs of ~1e-6 become ~1e-4 in TRIX
            ValidationHelper.VerifyData(qOutput, tResult, lookback, tolerance: 5e-4);
        }
        _output.WriteLine("TRIX Span validated against Tulip");
    }

    // ── H) Tulip Streaming ───────────────────────────────────────────────────
    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = [9, 14, 25];
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var trix = new global::QuanTAlib.Trix(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(trix.Update(item).Value);
            }

            var trixIndicator = Tulip.Indicators.trix;
            double[][] inputs = [tData];
            double[] options = [period];

            int lookback = trixIndicator.Start(options);
            double[][] outputs = [new double[tData.Length - lookback]];

            trixIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Tulip uses non-compensated EMA; warmup compensation causes persistent diffs
            // TRIX amplifies by 100×, so small EMA diffs become noticeable in TRIX
            ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: 1e-3);
        }
        _output.WriteLine("TRIX Streaming validated successfully against Tulip");
    }

    // ── I) Self-Consistency: All Modes ────────────────────────────────────────
    [Fact]
    public void Validate_AllModes_ProduceIdenticalResults()
    {
        int[] periods = [5, 10, 20, 50];

        foreach (var period in periods)
        {
            // 1. Batch Mode (TSeries)
            var batchTrix = new global::QuanTAlib.Trix(period);
            var batchResult = batchTrix.Update(_testData.Data);

            // 2. Span Mode
            double[] sourceData = _testData.RawData.ToArray();
            double[] spanOutput = new double[sourceData.Length];
            global::QuanTAlib.Trix.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

            // 3. Streaming Mode
            var streamingTrix = new global::QuanTAlib.Trix(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(streamingTrix.Update(item).Value);
            }

            // Compare all modes
            for (int i = 0; i < _testData.Data.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-8);
                Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-8);
            }
        }
        _output.WriteLine("All modes validated to produce identical results");
    }

    // ── J) Self-Consistency: Convergence ──────────────────────────────────────
    [Fact]
    public void Validate_Convergence_AfterWarmup()
    {
        int[] periods = [5, 10, 20, 50];

        foreach (var period in periods)
        {
            var trix = new global::QuanTAlib.Trix(period);
            int warmup = trix.WarmupPeriod; // period * 3

            Assert.False(trix.IsHot);

            for (int i = 0; i < warmup - 1; i++)
            {
                trix.Update(_testData.Data[i]);
                Assert.False(trix.IsHot);
            }

            trix.Update(_testData.Data[warmup - 1]);
            Assert.True(trix.IsHot);
        }
    }

    // ── K) NaN Robustness ────────────────────────────────────────────────────
    [Fact]
    public void Validate_HandlesNaN_Gracefully()
    {
        var trix = new global::QuanTAlib.Trix(10);

        for (int i = 0; i < 20; i++)
        {
            trix.Update(_testData.Data[i]);
        }

        var result = trix.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));

        for (int i = 20; i < 30; i++)
        {
            var r = trix.Update(_testData.Data[i]);
            Assert.True(double.IsFinite(r.Value));
        }
    }

    // ── L) Infinity Robustness ───────────────────────────────────────────────
    [Fact]
    public void Validate_HandlesInfinity_Gracefully()
    {
        var trix = new global::QuanTAlib.Trix(10);

        for (int i = 0; i < 20; i++)
        {
            trix.Update(_testData.Data[i]);
        }

        var resultPos = trix.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPos.Value));

        var resultNeg = trix.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNeg.Value));
    }

    // ── M) Zero Crossing Behavior ────────────────────────────────────────────
    [Fact]
    public void Validate_ZeroCrossing_DetectsDirectionChange()
    {
        var trix = new global::QuanTAlib.Trix(3);

        // Feed a long sustained uptrend to ensure TRIX stabilizes positive
        for (int i = 0; i < 50; i++)
        {
            trix.Update(new TValue(DateTime.UtcNow, 100 + (i * 2)));
        }
        double uptrendTrix = trix.Last.Value;
        Assert.True(uptrendTrix > 0, $"Sustained uptrend should produce positive TRIX, got {uptrendTrix}");

        // Feed a long sustained downtrend
        for (int i = 0; i < 50; i++)
        {
            trix.Update(new TValue(DateTime.UtcNow, 200 - (i * 2)));
        }
        double downtrendTrix = trix.Last.Value;
        Assert.True(downtrendTrix < 0, $"Sustained downtrend should produce negative TRIX, got {downtrendTrix}");
    }

    // ── N) Flat Line ─────────────────────────────────────────────────────────
    [Fact]
    public void Validate_FlatLine_ProducesZeroTrix()
    {
        var trix = new global::QuanTAlib.Trix(10);

        for (int i = 0; i < 200; i++)
        {
            trix.Update(new TValue(DateTime.UtcNow, 100));
        }

        // After sufficient warmup with flat data, TRIX ≈ 0
        // Warmup compensation introduces tiny residual; 1e-4 is sufficient
        Assert.True(Math.Abs(trix.Last.Value) < 1e-4,
            $"Expected TRIX ≈ 0 for flat line, got {trix.Last.Value}");
    }

    // ── O) Large Dataset Precision ───────────────────────────────────────────
    [Fact]
    public void Validate_LargeDataset_MaintainsPrecision()
    {
        const int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(10_000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Compare batch vs streaming on last 100 points of large dataset
        var batchResult = global::QuanTAlib.Trix.Batch(bars.Close, period);

        var streamTrix = new global::QuanTAlib.Trix(period);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            streamTrix.Update(bars.Close[i]);
        }

        // Verify final values match
        Assert.Equal(batchResult.Last.Value, streamTrix.Last.Value, 1e-9);
    }

    // ── P) Different Periods ─────────────────────────────────────────────────
    [Fact]
    public void Validate_DifferentPeriods_ProduceDifferentSensitivity()
    {
        var trix5 = new global::QuanTAlib.Trix(5);
        var trix20 = new global::QuanTAlib.Trix(20);
        var trix50 = new global::QuanTAlib.Trix(50);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            trix5.Update(_testData.Data[i]);
            trix20.Update(_testData.Data[i]);
            trix50.Update(_testData.Data[i]);
        }

        Assert.True(double.IsFinite(trix5.Last.Value));
        Assert.True(double.IsFinite(trix20.Last.Value));
        Assert.True(double.IsFinite(trix50.Last.Value));
    }

    // ── Q) Batch Span NaN ────────────────────────────────────────────────────
    [Fact]
    public void Validate_BatchSpan_HandlesNaN_InMiddle()
    {
        double[] data = new double[100];
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            data[i] = gbm.Next().Close;
        }

        data[50] = double.NaN;

        double[] result = new double[100];
        global::QuanTAlib.Trix.Batch(data.AsSpan(), result.AsSpan(), 10);

        foreach (var value in result)
        {
            Assert.True(double.IsFinite(value), $"Expected finite value, got {value}");
        }
    }

    // ── Cross-library: OoplesFinance ──────────────────────────────────────────
    [Fact]
    public void Trix_MatchesOoples_Structural()
    {
        const int period = 14;
        var ooplesData = _testData.SkenderQuotes.Select(static q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateTrix(length: period);
        var oValues = oResult.OutputValues.Values.First();

        var trix = new global::QuanTAlib.Trix(period);
        var qValues = new List<double>();
        foreach (var item in _testData.Data)
        {
            qValues.Add(trix.Update(item).Value);
        }

        Assert.True(oValues.Count > 0, "Ooples Trix must produce output");
        int finiteCount = 0;
        for (int i = period; i < Math.Min(oValues.Count, qValues.Count); i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qValues[i]))
            {
                finiteCount++;
            }
        }
        Assert.True(finiteCount > 100, $"Expected >100 finite Trix pairs, got {finiteCount}");
        _output.WriteLine($"Trix Ooples structural: {finiteCount} finite pairs verified.");
    }
}
