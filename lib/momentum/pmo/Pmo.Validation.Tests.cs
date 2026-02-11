using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for PMO (Price Momentum Oscillator) against external libraries.
/// PMO applies double EMA smoothing to the Rate of Change.
///
/// Skender has GetPmo(). Ooples has CalculatePriceMomentumOscillator().
/// </summary>
public sealed class PmoValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int RocPeriod = 35;
    private const int Smooth1Period = 20;
    private const int SignalPeriod = 10;
    private const double Tolerance = 1e-10;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    #region Skender Validation

    [Fact]
    public void Pmo_MatchesSkender_Batch()
    {
        // QuanTAlib PMO
        var qResult = Pmo.Batch(_testData.Data, RocPeriod, Smooth1Period, SignalPeriod);

        // Skender PMO
        var sResult = _testData.SkenderQuotes.GetPmo(RocPeriod, Smooth1Period, SignalPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Pmo);

        _output.WriteLine("PMO Batch validated successfully against Skender");
    }

    [Fact]
    public void Pmo_MatchesSkender_Streaming()
    {
        // QuanTAlib PMO (streaming)
        var pmo = new Pmo(RocPeriod, Smooth1Period, SignalPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(pmo.Update(item).Value);
        }

        // Skender PMO
        var sResult = _testData.SkenderQuotes.GetPmo(RocPeriod, Smooth1Period, SignalPeriod).ToList();

        int count = qResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            if (sResult[i].Pmo is null) { continue; }
            Assert.True(
                Math.Abs(qResults[i] - sResult[i].Pmo!.Value) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Skender={sResult[i].Pmo:G17}");
        }

        _output.WriteLine("PMO Streaming validated successfully against Skender");
    }

    [Theory]
    [InlineData(10, 10, 5)]
    [InlineData(35, 20, 10)]
    [InlineData(50, 30, 15)]
    public void Pmo_MatchesSkender_DifferentPeriods(int rocPeriod, int smooth1, int signal)
    {
        var qResult = Pmo.Batch(_testData.Data, rocPeriod, smooth1, signal);

        var sResult = _testData.SkenderQuotes.GetPmo(rocPeriod, smooth1, signal).ToList();

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Pmo);
    }

    #endregion

    #region Ooples Validation

    [Fact]
    public void Pmo_MatchesOoples_Batch()
    {
        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        // QuanTAlib PMO
        var qResult = Pmo.Batch(_testData.Data, RocPeriod, Smooth1Period, SignalPeriod);

        // Ooples PMO (DecisionPoint variant uses same algorithm)
        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculatePriceMomentumOscillator(
            length1: RocPeriod, length2: Smooth1Period, signalLength: SignalPeriod);
        var oValues = oResult.OutputValues.Values.First();

        int count = qResult.Count;
        int warmup = RocPeriod + Smooth1Period + SignalPeriod;
        int start = Math.Max(warmup, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(qResult[i].Value - oValues[i]) <= ValidationHelper.OoplesTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResult[i].Value:G17}, Ooples={oValues[i]:G17}");
        }

        _output.WriteLine("PMO Batch validated successfully against Ooples");
    }

    #endregion

    #region Self-Consistency

    [Fact]
    public void Pmo_BatchAndStreaming_AreIdentical()
    {
        // Batch
        var batchResult = Pmo.Batch(_testData.Data, RocPeriod, Smooth1Period, SignalPeriod);

        // Streaming
        var pmo = new Pmo(RocPeriod, Smooth1Period, SignalPeriod);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            streamingResults.Add(pmo.Update(item).Value);
        }

        int count = _testData.Data.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], Tolerance);
        }
        _output.WriteLine("PMO Batch vs Streaming consistency validated");
    }

    [Fact]
    public void Pmo_SpanAndBatch_AreIdentical()
    {
        // Batch TSeries
        var batchResult = Pmo.Batch(_testData.Data, RocPeriod, Smooth1Period, SignalPeriod);

        // Span
        double[] rawData = _testData.RawData.ToArray();
        var spanOutput = new double[rawData.Length];
        Pmo.Batch(rawData, spanOutput, RocPeriod, Smooth1Period, SignalPeriod);

        int count = rawData.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], Tolerance);
        }
        _output.WriteLine("PMO Span vs Batch consistency validated");
    }

    [Theory]
    [InlineData(5, 3, 3)]
    [InlineData(10, 10, 5)]
    [InlineData(35, 20, 10)]
    [InlineData(50, 30, 15)]
    public void Pmo_DifferentParameters_BatchStreamingConsistency(int rocPeriod, int smooth1, int smooth2)
    {
        var batchResult = Pmo.Batch(_testData.Data, rocPeriod, smooth1, smooth2);

        var pmo = new Pmo(rocPeriod, smooth1, smooth2);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            streamingResults.Add(pmo.Update(item).Value);
        }

        int count = _testData.Data.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], Tolerance);
        }
    }

    #endregion

    #region Known Value Tests

    [Fact]
    public void Pmo_ConstantInput_ConvergesToZero()
    {
        // With constant prices, ROC% = 0, so PMO should converge to 0
        var pmo = new Pmo(5, 3, 3);

        for (int i = 0; i < 100; i++)
        {
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        Assert.True(Math.Abs(pmo.Last.Value) < 1e-6,
            $"PMO should converge to 0 for constant input, got {pmo.Last.Value}");
    }

    [Fact]
    public void Pmo_StrongUptrend_ProducesPositive()
    {
        var pmo = new Pmo(5, 3, 3);

        for (int i = 0; i < 50; i++)
        {
            double price = 100 + i * 5; // Strong uptrend
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price), true);
        }

        Assert.True(pmo.Last.Value > 0,
            $"PMO should be positive during strong uptrend, got {pmo.Last.Value}");
    }

    [Fact]
    public void Pmo_StrongDowntrend_ProducesNegative()
    {
        var pmo = new Pmo(5, 3, 3);

        for (int i = 0; i < 50; i++)
        {
            double price = 200 - i * 3; // Strong downtrend
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price), true);
        }

        Assert.True(pmo.Last.Value < 0,
            $"PMO should be negative during strong downtrend, got {pmo.Last.Value}");
    }

    [Fact]
    public void Pmo_ResetClearsState()
    {
        var pmo = new Pmo(5, 3, 3);

        // Run once
        foreach (var item in _testData.Data)
        {
            pmo.Update(item);
        }

        var firstRunLast = pmo.Last.Value;
        pmo.Reset();

        // Run again - should produce identical results
        foreach (var item in _testData.Data)
        {
            pmo.Update(item);
        }

        Assert.Equal(firstRunLast, pmo.Last.Value, Tolerance);
    }

    #endregion

    #region Behavioral Tests

    [Fact]
    public void Pmo_RespondsToSmoothingPeriods()
    {
        // Short smoothing = more responsive = higher amplitude
        var pmoFast = new Pmo(5, 3, 2);
        var pmoSlow = new Pmo(5, 20, 10);

        double sumAbsFast = 0;
        double sumAbsSlow = 0;

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            pmoFast.Update(_testData.Data[i]);
            pmoSlow.Update(_testData.Data[i]);

            if (i >= 50) // After warmup
            {
                sumAbsFast += Math.Abs(pmoFast.Last.Value);
                sumAbsSlow += Math.Abs(pmoSlow.Last.Value);
            }
        }

        Assert.True(sumAbsFast > sumAbsSlow,
            $"Fast PMO ({sumAbsFast:F4}) should have higher amplitude than slow PMO ({sumAbsSlow:F4})");
    }

    [Fact]
    public void Pmo_AllOutputsFiniteAfterWarmup()
    {
        var pmo = new Pmo(RocPeriod, Smooth1Period, SignalPeriod);

        foreach (var item in _testData.Data)
        {
            pmo.Update(item);
            Assert.True(double.IsFinite(pmo.Last.Value),
                $"PMO output should be finite, got {pmo.Last.Value}");
        }
    }

    [Fact]
    public void Pmo_RocPeriodAffectsOutput()
    {
        var pmo5 = new Pmo(5, 10, 5);
        var pmo20 = new Pmo(20, 10, 5);

        foreach (var item in _testData.Data)
        {
            pmo5.Update(item);
            pmo20.Update(item);
        }

        // Different ROC periods should produce different results
        Assert.NotEqual(pmo5.Last.Value, pmo20.Last.Value);
    }

    #endregion
}
