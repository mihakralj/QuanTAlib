using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for TSI (True Strength Index) against external libraries.
/// TSI = 100 × EMA(EMA(momentum, long), short) / EMA(EMA(|momentum|, long), short)
/// Signal line: EMA(TSI, signalPeriod)
///
/// Skender has GetTsi(). Ooples has CalculateTrueStrengthIndex().
/// </summary>
public sealed class TsiValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int LongPeriod = 25;
    private const int ShortPeriod = 13;
    private const int SignalPeriod = 13;

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
    public void Tsi_MatchesSkender_Batch()
    {
        // QuanTAlib TSI
        var qResult = Tsi.Batch(_testData.Data, LongPeriod, ShortPeriod, SignalPeriod);

        // Skender TSI
        var sResult = _testData.SkenderQuotes.GetTsi(LongPeriod, ShortPeriod, SignalPeriod).ToList();

        // Compare last 100 records (skip warmup)
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Tsi);

        _output.WriteLine("TSI Batch validated successfully against Skender");
    }

    [Fact]
    public void Tsi_MatchesSkender_Streaming()
    {
        // QuanTAlib TSI (streaming)
        var tsi = new Tsi(LongPeriod, ShortPeriod, SignalPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(tsi.Update(item).Value);
        }

        // Skender TSI
        var sResult = _testData.SkenderQuotes.GetTsi(LongPeriod, ShortPeriod, SignalPeriod).ToList();

        int count = qResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            if (sResult[i].Tsi is null) { continue; }
            Assert.True(
                Math.Abs(qResults[i] - sResult[i].Tsi!.Value) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Skender={sResult[i].Tsi:G17}");
        }

        _output.WriteLine("TSI Streaming validated successfully against Skender");
    }

    [Theory]
    [InlineData(13, 7, 7)]
    [InlineData(25, 13, 13)]
    [InlineData(40, 20, 10)]
    public void Tsi_MatchesSkender_DifferentPeriods(int longPeriod, int shortPeriod, int signalPeriod)
    {
        var qResult = Tsi.Batch(_testData.Data, longPeriod, shortPeriod, signalPeriod);

        var sResult = _testData.SkenderQuotes.GetTsi(longPeriod, shortPeriod, signalPeriod).ToList();

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Tsi);
    }

    #endregion

    #region Ooples Validation

    [Fact]
    public void Tsi_MatchesOoples_Batch()
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

        // QuanTAlib TSI
        var qResult = Tsi.Batch(_testData.Data, LongPeriod, ShortPeriod, SignalPeriod);

        // Ooples TSI
        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateTrueStrengthIndex(length1: LongPeriod, length2: ShortPeriod, signalLength: SignalPeriod);
        var oValues = oResult.OutputValues.Values.First();

        int count = qResult.Count;
        int warmup = LongPeriod + ShortPeriod + SignalPeriod;
        int start = Math.Max(warmup, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(qResult[i].Value - oValues[i]) <= ValidationHelper.OoplesTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResult[i].Value:G17}, Ooples={oValues[i]:G17}");
        }

        _output.WriteLine("TSI Batch validated successfully against Ooples");
    }

    #endregion

    #region Formula Validation

    [Fact]
    public void Tsi_ConstantPositiveMomentum_ApproachesPositive100()
    {
        var tsi = new Tsi(3, 2, 2);

        for (int i = 0; i < 50; i++)
        {
            tsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (i * 2)));
        }

        Assert.True(tsi.Last.Value > 95.0, $"Expected TSI > 95, got {tsi.Last.Value}");
    }

    [Fact]
    public void Tsi_ConstantNegativeMomentum_ApproachesNegative100()
    {
        var tsi = new Tsi(3, 2, 2);

        for (int i = 0; i < 50; i++)
        {
            tsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 200.0 - (i * 2)));
        }

        Assert.True(tsi.Last.Value < -95.0, $"Expected TSI < -95, got {tsi.Last.Value}");
    }

    [Fact]
    public void Tsi_NoChange_ApproachesZero()
    {
        var tsi = new Tsi(3, 2, 2);

        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        Assert.True(Math.Abs(tsi.Last.Value) < 1.0, $"Expected TSI ≈ 0, got {tsi.Last.Value}");
    }

    [Fact]
    public void Tsi_SignalLagsMainLine()
    {
        var tsi = new Tsi(5, 3, 3);
        var tsiValues = new List<double>();
        var signalValues = new List<double>();

        for (int i = 0; i < 20; i++)
        {
            double price = i < 10 ? 100.0 + (i * 2) : 120.0 - ((i - 10) * 2);
            tsi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
            tsiValues.Add(tsi.Last.Value);
            signalValues.Add(tsi.Signal);
        }

        // Signal should lag TSI
        var diff = tsiValues.Zip(signalValues, (t, s) => t - s).ToList();
        double avgDiff = diff.Average();
        double variance = diff.Average(d => (d - avgDiff) * (d - avgDiff));

        Assert.True(variance > 0.001, "Signal should lag TSI, showing variance in differences");
    }

    [Fact]
    public void Tsi_RangeIsBounded()
    {
        var tsi = new Tsi(LongPeriod, ShortPeriod, SignalPeriod);
        const double epsilon = 1e-10;

        foreach (var item in _testData.Data)
        {
            tsi.Update(item);
            Assert.True(tsi.Last.Value >= -100 - epsilon && tsi.Last.Value <= 100 + epsilon,
                $"TSI value {tsi.Last.Value} out of range [-100, 100]");
        }
    }

    #endregion

    #region Consistency Validation

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        // TSI uses triple EMA smoothing (long EMA → short EMA → signal EMA),
        // so batch vs streaming modes diverge during warmup due to different
        // initialization paths. Compare only well-converged tail values.
        const double convergenceTolerance = 1e-6;

        // Batch
        var batchResult = Tsi.Batch(_testData.Data, LongPeriod, ShortPeriod, SignalPeriod);

        // Streaming
        var tsi = new Tsi(LongPeriod, ShortPeriod, SignalPeriod);
        var streamingResults = new List<double>();
        foreach (var value in _testData.Data)
        {
            streamingResults.Add(tsi.Update(value).Value);
        }

        // Skip early warmup region where initialization paths diverge
        int count = _testData.Data.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(batchResult.Values[i] - streamingResults[i]) <= convergenceTolerance,
                $"Mismatch at index {i}: Batch={batchResult.Values[i]:G17}, Streaming={streamingResults[i]:G17}");
        }
        _output.WriteLine("TSI Batch vs Streaming consistency validated");
    }

    [Fact]
    public void Tsi_ResetProducesIdenticalResults()
    {
        var tsi = new Tsi(LongPeriod, ShortPeriod, SignalPeriod);

        // First run
        foreach (var item in _testData.Data)
        {
            tsi.Update(item);
        }
        var firstValue = tsi.Last.Value;
        var firstSignal = tsi.Signal;

        tsi.Reset();

        // Second run
        foreach (var item in _testData.Data)
        {
            tsi.Update(item);
        }

        Assert.Equal(firstValue, tsi.Last.Value, 1e-10);
        Assert.Equal(firstSignal, tsi.Signal, 1e-10);
    }

    #endregion
}
