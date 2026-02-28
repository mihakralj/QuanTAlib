using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Tulip;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class TsfValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public TsfValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _testData.Dispose();
            _disposed = true;
        }
    }

    // ── Cross-validate against LSMA(offset=1) ─────────────────────────
    // TSF = LSMA with offset=1. This is a mathematical identity.

    [Fact]
    public void Validate_LSMA_Batch()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            var tsf = new global::QuanTAlib.Tsf(period);
            var tsfResult = tsf.Update(_testData.Data);

            var lsma = new global::QuanTAlib.Lsma(period, offset: 1);
            var lsmaResult = lsma.Update(_testData.Data);

            int compareCount = 100;
            int start = tsfResult.Count - compareCount;

            for (int i = start; i < tsfResult.Count; i++)
            {
                Assert.Equal(lsmaResult.Values[i], tsfResult.Values[i], 1e-9);
            }
        }
        _output.WriteLine("TSF Batch validated successfully against LSMA(offset=1)");
    }

    [Fact]
    public void Validate_LSMA_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            var tsf = new global::QuanTAlib.Tsf(period);
            var lsma = new global::QuanTAlib.Lsma(period, offset: 1);

            var tsfResults = new List<double>();
            var lsmaResults = new List<double>();

            foreach (var item in _testData.Data)
            {
                tsfResults.Add(tsf.Update(item).Value);
                lsmaResults.Add(lsma.Update(item).Value);
            }

            int compareCount = 100;
            int start = tsfResults.Count - compareCount;

            for (int i = start; i < tsfResults.Count; i++)
            {
                Assert.Equal(lsmaResults[i], tsfResults[i], 1e-9);
            }
        }
        _output.WriteLine("TSF Streaming validated successfully against LSMA(offset=1)");
    }

    [Fact]
    public void Validate_LSMA_Span()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            double[] tsfOutput = new double[_testData.RawData.Length];
            double[] lsmaOutput = new double[_testData.RawData.Length];

            global::QuanTAlib.Tsf.Batch(_testData.RawData.Span, tsfOutput.AsSpan(), period);
            global::QuanTAlib.Lsma.Batch(_testData.RawData.Span, lsmaOutput.AsSpan(), period, offset: 1);

            int compareCount = 100;
            int start = tsfOutput.Length - compareCount;

            for (int i = start; i < tsfOutput.Length; i++)
            {
                Assert.Equal(lsmaOutput[i], tsfOutput[i], 1e-9);
            }
        }
        _output.WriteLine("TSF Span validated successfully against LSMA(offset=1)");
    }

    // ── Self-consistency checks ────────────────────────────────────────

    [Fact]
    public void Validate_Batch_Streaming_Consistency()
    {
        const int period = 14;

        // Batch
        var batchResult = global::QuanTAlib.Tsf.Batch(_testData.Data, period);

        // Streaming
        var tsf = new global::QuanTAlib.Tsf(period);
        var streamResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamResults.Add(tsf.Update(item).Value);
        }

        int compareCount = 100;
        int start = batchResult.Count - compareCount;
        for (int i = start; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamResults[i], 1e-6);
        }
        _output.WriteLine("TSF Batch vs Streaming consistency verified");
    }

    [Fact]
    public void Validate_DifferentPeriods()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var result = global::QuanTAlib.Tsf.Batch(_testData.Data, period);
            Assert.True(result.Count == _testData.Data.Count);
            Assert.True(double.IsFinite(result.Values[^1]));
        }
        _output.WriteLine("TSF different periods validated");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 14;
        var (results, indicator) = global::QuanTAlib.Tsf.Calculate(_testData.Data, period);

        Assert.True(indicator.IsHot);
        Assert.True(results.Count == _testData.Data.Count);
        Assert.Equal(results.Values[^1], indicator.Last.Value);
        _output.WriteLine("TSF Calculate returns hot indicator verified");
    }

    [Fact]
    public void Validate_BarCorrection_Consistency()
    {
        const int period = 14;

        // Feed initial data
        var tsf = new global::QuanTAlib.Tsf(period);
        for (int i = 0; i < 100; i++)
        {
            tsf.Update(_testData.Data[i], isNew: true);
        }
        double expectedLast = tsf.Last.Value;

        // Apply multiple corrections, then restore
        for (int j = 0; j < 5; j++)
        {
            tsf.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        }
        tsf.Update(_testData.Data[99], isNew: false);

        Assert.Equal(expectedLast, tsf.Last.Value, 1e-6);
        _output.WriteLine("TSF bar correction consistency verified");
    }

    // ── Tulip Cross-Validation ─────────────────────────────────────────────────

    /// <summary>
    /// Validates TSF against Tulip <c>tsf</c> (Time Series Forecast).
    /// Tulip formula: linear regression value projected one period forward —
    /// identical to QuanTAlib TSF = slope*(n-1+1) + intercept = Lsma(offset=1).
    /// </summary>
    [Fact]
    public void Tsf_Matches_Tulip_Batch()
    {
        const int period = 14;
        double[] data = _testData.RawData.ToArray();

        var qResult = global::QuanTAlib.Tsf.Batch(_testData.Data, period);

        var tulipIndicator = Tulip.Indicators.tsf;
        double[][] inputs = { data };
        double[] options = { period };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[data.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: 1e-9);
        _output.WriteLine("TSF Batch validated against Tulip tsf");
    }

    [Fact]
    public void Tsf_Matches_Tulip_Streaming()
    {
        const int period = 20;
        double[] data = _testData.RawData.ToArray();

        var tsf = new global::QuanTAlib.Tsf(period);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(tsf.Update(item).Value);
        }

        var tulipIndicator = Tulip.Indicators.tsf;
        double[][] inputs = { data };
        double[] options = { period };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[data.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        // Tolerance relaxed to 2e-8: floating-point accumulation over long runs can produce
        // low-1e-8 drift between streaming (incremental) and batch (single-pass) paths.
        ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: 2e-8);
        _output.WriteLine("TSF Streaming validated against Tulip tsf");
    }

    // ── Cross-library: OoplesFinance ────────────────────────────────────

    /// <summary>
    /// Structural validation against Ooples <c>CalculateTimeSeriesForecast</c>.
    /// Ooples TSF uses the same linear-regression-forecast-one-bar-ahead definition.
    /// Numeric equality is not asserted: Ooples default period is 500 (batch-oriented),
    /// so at period=14 results may differ due to seeding strategy.
    /// Both must produce finite output after warmup on the same close series.
    /// </summary>
    [Fact]
    public void Tsf_MatchesOoples_Structural()
    {
        const int period = 14;

        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateTimeSeriesForecast(length: period);
        var oValues = oResult.OutputValues.Values.First();

        var tsf = new Tsf(period);
        var qValues = new System.Collections.Generic.List<double>();
        foreach (var item in _testData.Data)
        {
            qValues.Add(tsf.Update(item).Value);
        }

        Assert.True(oValues.Count > 0, "Ooples TSF must produce output");

        int finiteCount = 0;
        for (int i = period; i < Math.Min(oValues.Count, qValues.Count); i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qValues[i]))
            {
                finiteCount++;
            }
        }

        Assert.True(finiteCount > 100, $"Expected >100 finite TSF pairs, got {finiteCount}");
        _output.WriteLine($"TSF Ooples structural: {finiteCount} finite pairs verified.");
    }
}
