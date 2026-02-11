using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Xunit;
using Xunit.Abstractions;
using TALib;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for PPO (Percentage Price Oscillator) against external libraries.
/// Tulip has a 'ppo' indicator.
/// TA-Lib has PPO function.
/// Ooples has CalculatePercentagePriceOscillator().
/// Skender does not have a PPO indicator.
/// </summary>
public sealed class PpoValidationTests(ITestOutputHelper output) : IDisposable
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
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    #region Tulip PPO Validation

    [Fact]
    public void Ppo_MatchesTulipPpo_Streaming()
    {
        // Tulip has hardcoded alpha overrides for 12/26, use different periods
        const int fastPeriod = 10;
        const int slowPeriod = 20;
        const int signalPeriod = 9;

        double[] tData = _testData.RawData.ToArray();

        // Calculate QuanTAlib PPO (streaming)
        var ppo = new global::QuanTAlib.Ppo(fastPeriod, slowPeriod, signalPeriod);
        var qPpo = new List<double>();

        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
            qPpo.Add(ppo.Last.Value);
        }

        // Calculate Tulip PPO
        var ppoIndicator = Tulip.Indicators.ppo;
        double[][] inputs = [tData];
        double[] options = [fastPeriod, slowPeriod];

        int lookback = ppoIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        ppoIndicator.Run(inputs, options, outputs);
        var tPpo = outputs[0];

        // Compare last 100 records
        ValidationHelper.VerifyData(qPpo, tPpo, lookback);

        _output.WriteLine("PPO Streaming validated successfully against Tulip");
    }

    [Theory]
    [InlineData(5, 15)]
    [InlineData(8, 21)]
    [InlineData(10, 20)]
    [InlineData(15, 30)]
    public void Ppo_MatchesTulipPpo_DifferentPeriods(int fastPeriod, int slowPeriod)
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib PPO
        var ppo = new global::QuanTAlib.Ppo(fastPeriod, slowPeriod, 9);
        var qPpo = new List<double>();

        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
            qPpo.Add(ppo.Last.Value);
        }

        // Tulip PPO
        var ppoIndicator = Tulip.Indicators.ppo;
        double[][] inputs = [tData];
        double[] options = [fastPeriod, slowPeriod];

        int lookback = ppoIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        ppoIndicator.Run(inputs, options, outputs);
        var tPpo = outputs[0];

        ValidationHelper.VerifyData(qPpo, tPpo, lookback);
    }

    #endregion

    #region TA-Lib PPO Validation

    [Fact]
    public void Ppo_MatchesTalib_Streaming()
    {
        const int fastPeriod = 12;
        const int slowPeriod = 26;

        double[] tData = _testData.RawData.ToArray();
        double[] outPpo = new double[tData.Length];

        // QuanTAlib PPO (streaming)
        var ppo = new global::QuanTAlib.Ppo(fastPeriod, slowPeriod, 9);
        var qPpo = new List<double>();

        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
            qPpo.Add(ppo.Last.Value);
        }

        // TA-Lib PPO (must specify MAType.Ema — default is SMA which differs from our EMA-based PPO)
        var retCode = TALib.Functions.Ppo<double>(tData, 0..^0, outPpo, out var outRange, fastPeriod, slowPeriod, Core.MAType.Ema);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.PpoLookback(fastPeriod, slowPeriod, Core.MAType.Ema);

        // Compare
        ValidationHelper.VerifyData(qPpo, outPpo, outRange, lookback);

        _output.WriteLine("PPO Streaming validated successfully against TA-Lib");
    }

    #endregion

    #region Ooples Validation

    [Fact]
    public void Ppo_MatchesOoples_Batch()
    {
        const int fastPeriod = 12;
        const int slowPeriod = 26;
        const int signalPeriod = 9;

        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        // QuanTAlib PPO
        var ppo = new global::QuanTAlib.Ppo(fastPeriod, slowPeriod, signalPeriod);
        var qPpo = new List<double>();

        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
            qPpo.Add(ppo.Last.Value);
        }

        // Ooples PPO
        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculatePercentagePriceOscillator(
            fastLength: fastPeriod, slowLength: slowPeriod, signalLength: signalPeriod);
        var oValues = oResult.OutputValues.Values.First();

        int count = qPpo.Count;
        int warmup = slowPeriod + signalPeriod;
        int start = Math.Max(warmup, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(qPpo[i] - oValues[i]) <= ValidationHelper.OoplesTolerance,
                $"Mismatch at index {i}: QuanTAlib={qPpo[i]:G17}, Ooples={oValues[i]:G17}");
        }

        _output.WriteLine("PPO Batch validated successfully against Ooples");
    }

    #endregion

    #region Self-Consistency

    [Fact]
    public void Ppo_BatchAndStreaming_AreIdentical()
    {
        const int fastPeriod = 12;
        const int slowPeriod = 26;
        const int signalPeriod = 9;

        // Batch
        var batchResult = global::QuanTAlib.Ppo.Batch(_testData.Data, fastPeriod, slowPeriod, signalPeriod);

        // Streaming
        var ppo = new global::QuanTAlib.Ppo(fastPeriod, slowPeriod, signalPeriod);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
            streamingResults.Add(ppo.Last.Value);
        }

        // They must match exactly
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-10);
        }
    }

    [Fact]
    public void Ppo_HistogramEqualsLineMinusSignal()
    {
        const int fastPeriod = 12;
        const int slowPeriod = 26;
        const int signalPeriod = 9;

        var ppo = new global::QuanTAlib.Ppo(fastPeriod, slowPeriod, signalPeriod);

        foreach (var item in _testData.Data)
        {
            ppo.Update(item);

            double line = ppo.Last.Value;
            double signal = ppo.Signal.Value;
            double hist = ppo.Histogram.Value;

            Assert.Equal(line - signal, hist, 1e-10);
        }
    }

    [Fact]
    public void Ppo_ConstantInput_ConvergesToZero()
    {
        var ppo = new global::QuanTAlib.Ppo(12, 26, 9);

        for (int i = 0; i < 200; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        Assert.True(Math.Abs(ppo.Last.Value) < 1e-6,
            $"PPO should converge to 0 for constant input, got {ppo.Last.Value}");
        Assert.True(Math.Abs(ppo.Signal.Value) < 1e-6,
            $"Signal should converge to 0 for constant input, got {ppo.Signal.Value}");
        Assert.True(Math.Abs(ppo.Histogram.Value) < 1e-6,
            $"Histogram should converge to 0 for constant input, got {ppo.Histogram.Value}");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Ppo_AllOutputsFiniteAfterWarmup()
    {
        var ppo = new global::QuanTAlib.Ppo(12, 26, 9);

        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
            Assert.True(double.IsFinite(ppo.Last.Value),
                $"PPO output should be finite, got {ppo.Last.Value}");
            Assert.True(double.IsFinite(ppo.Signal.Value),
                $"Signal output should be finite, got {ppo.Signal.Value}");
            Assert.True(double.IsFinite(ppo.Histogram.Value),
                $"Histogram output should be finite, got {ppo.Histogram.Value}");
        }
    }

    [Fact]
    public void Ppo_ResetProducesIdenticalResults()
    {
        const int fastPeriod = 12;
        const int slowPeriod = 26;
        const int signalPeriod = 9;

        var ppo = new global::QuanTAlib.Ppo(fastPeriod, slowPeriod, signalPeriod);

        // First run
        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
        }

        var firstPpo = ppo.Last.Value;
        var firstSignal = ppo.Signal.Value;
        var firstHist = ppo.Histogram.Value;

        ppo.Reset();

        // Second run
        foreach (var item in _testData.Data)
        {
            ppo.Update(item);
        }

        Assert.Equal(firstPpo, ppo.Last.Value, 1e-10);
        Assert.Equal(firstSignal, ppo.Signal.Value, 1e-10);
        Assert.Equal(firstHist, ppo.Histogram.Value, 1e-10);
    }

    #endregion
}
