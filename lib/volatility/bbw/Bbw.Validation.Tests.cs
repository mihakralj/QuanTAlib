using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for BBW (Bollinger Band Width).
/// Compares against Skender's BollingerBands implementation.
/// </summary>
public sealed class BbwValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public BbwValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 20 };
        double[] multipliers = { 2.0 };

        foreach (var period in periods)
        {
            foreach (var multiplier in multipliers)
            {
                // Calculate QuanTAlib BBW (batch TSeries) using Close prices
                var bbw = new global::QuanTAlib.Bbw(period, multiplier);
                var qResult = bbw.Update(_testData.Bars.Close);

                // Calculate Skender Bollinger Bands (width = upper - lower)
                var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

                // Compare last 100 records (using Width property from Skender)
                ValidationHelper.VerifyData(qResult, sResult, (s) => s.Width, tolerance: ValidationHelper.SkenderTolerance);
            }
        }
        _output.WriteLine("BBW Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 20 };
        double[] multipliers = { 2.0 };

        foreach (var period in periods)
        {
            foreach (var multiplier in multipliers)
            {
                // Calculate QuanTAlib BBW (streaming) using Close prices
                var bbw = new global::QuanTAlib.Bbw(period, multiplier);
                var qResults = new List<double>();
                foreach (var item in _testData.Bars.Close)
                {
                    qResults.Add(bbw.Update(item).Value);
                }

                // Calculate Skender Bollinger Bands (width = upper - lower)
                var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

                // Compare last 100 records
                ValidationHelper.VerifyData(qResults, sResult, (s) => s.Width, tolerance: ValidationHelper.SkenderTolerance);
            }
        }
        _output.WriteLine("BBW Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 20 };
        double[] multipliers = { 2.0 };

        // Prepare Close price data
        var closeData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        var output = new double[closeData.Length];

        foreach (var period in periods)
        {
            foreach (var multiplier in multipliers)
            {
                // Calculate QuanTAlib BBW (Span API)
                global::QuanTAlib.Bbw.Batch(closeData, output, period, multiplier);

                // Calculate Skender Bollinger Bands (width = upper - lower)
                var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

                // Compare last 100 records
                int lookback = period - 1;
                int startIndex = Math.Max(0, closeData.Length - 100);
                int skenderStartIndex = Math.Max(0, sResult.Count - 100);

                for (int i = 0; i < Math.Min(100, closeData.Length - lookback); i++)
                {
                    int qIdx = startIndex + i;
                    int sIdx = skenderStartIndex + i;

                    if (qIdx >= lookback && sIdx < sResult.Count && sResult[sIdx].Width.HasValue)
                    {
                        Assert.Equal(sResult[sIdx].Width!.Value, output[qIdx], ValidationHelper.SkenderTolerance);
                    }
                }
            }
        }
        _output.WriteLine("BBW Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_DifferentPeriods()
    {
        int[] periods = { 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib BBW
            var bbw = new global::QuanTAlib.Bbw(period);
            var qResult = bbw.Update(_testData.Bars.Close);

            // Calculate Skender Bollinger Bands
            var sResult = _testData.SkenderQuotes.GetBollingerBands(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Width, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("BBW validated successfully for different periods against Skender");
    }

    [Fact]
    public void Validate_DifferentMultipliers()
    {
        double[] multipliers = { 1.0, 1.5, 2.0, 2.5, 3.0 };
        int period = 20;

        foreach (var multiplier in multipliers)
        {
            // Calculate QuanTAlib BBW
            var bbw = new global::QuanTAlib.Bbw(period, multiplier);
            var qResult = bbw.Update(_testData.Bars.Close);

            // Calculate Skender Bollinger Bands
            var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Width, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("BBW validated successfully for different multipliers against Skender");
    }

    [Fact]
    public void Validate_StreamingBatchParity()
    {
        int period = 20;
        double multiplier = 2.0;

        // Streaming calculation
        var bbwStreaming = new global::QuanTAlib.Bbw(period, multiplier);
        var streamingResults = new List<double>();
        foreach (var item in _testData.Bars.Close)
        {
            streamingResults.Add(bbwStreaming.Update(item).Value);
        }

        // Batch calculation
        var bbwBatch = new global::QuanTAlib.Bbw(period, multiplier);
        var batchResult = bbwBatch.Update(_testData.Bars.Close);

        // Compare all records
        Assert.Equal(streamingResults.Count, batchResult.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult[i].Value, 1e-10);
        }
        _output.WriteLine("BBW streaming/batch parity validated successfully");
    }

    [Fact]
    public void Validate_SpanBatchParity()
    {
        int period = 20;
        double multiplier = 2.0;

        // Prepare Close price data
        var closeData = _testData.Bars.Close.Select(x => x.Value).ToArray();

        // Span calculation
        var spanOutput = new double[closeData.Length];
        global::QuanTAlib.Bbw.Batch(closeData, spanOutput, period, multiplier);

        // Instance batch calculation
        var bbw = new global::QuanTAlib.Bbw(period, multiplier);
        var batchResult = bbw.Update(_testData.Bars.Close);

        // Compare all records
        Assert.Equal(spanOutput.Length, batchResult.Count);
        for (int i = 0; i < spanOutput.Length; i++)
        {
            Assert.Equal(spanOutput[i], batchResult[i].Value, 1e-10);
        }
        _output.WriteLine("BBW span/batch parity validated successfully");
    }

    // ── Cross-library: OoplesFinance ──────────────────────────────────────────
    [Fact]
    public void Bbw_MatchesOoples_Structural()
    {
        const int period = 20;
        const double multiplier = 2.0;
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
        var oResult = stockData.CalculateBollingerBandsWidth(length: period);
        var oValues = oResult.OutputValues.Values.First();

        var bbw = new global::QuanTAlib.Bbw(period, multiplier);
        var qValues = new List<double>();
        foreach (var item in _testData.Data)
        {
            qValues.Add(bbw.Update(item).Value);
        }

        Assert.True(oValues.Count > 0, "Ooples BBW must produce output");
        int finiteCount = 0;
        for (int i = period; i < Math.Min(oValues.Count, qValues.Count); i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qValues[i]))
            {
                finiteCount++;
            }
        }
        Assert.True(finiteCount > 100, $"Expected >100 finite BBW pairs, got {finiteCount}");
        _output.WriteLine($"BBW Ooples structural: {finiteCount} finite pairs verified.");
    }
}