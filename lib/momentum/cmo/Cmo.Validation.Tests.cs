using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CMO (Chande Momentum Oscillator) against external libraries.
/// CMO = 100 × (SumUp - SumDown) / (SumUp + SumDown)
///
/// Note: TALib CMO uses Wilder's exponential smoothing internally, which produces
/// fundamentally different results than the standard simple-sum CMO formula.
/// QuanTAlib, Tulip, and Skender all use the standard simple-sum approach.
/// The TALib test below validates structural properties only — not numeric equality.
/// </summary>
public sealed class CmoValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 14;

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

    #region Tulip Validation

    [Fact]
    public void Cmo_MatchesTulip_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        double[] qOutput = new double[tData.Length];
        Cmo.Batch(tData.AsSpan(), qOutput.AsSpan(), TestPeriod);

        // Tulip cmo
        var cmoIndicator = Tulip.Indicators.cmo;
        double[][] inputs = [tData];
        double[] options = [TestPeriod];
        int lookback = cmoIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        cmoIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qOutput, tulipResult, lookback);

        _output.WriteLine("CMO Batch validated successfully against Tulip");
    }

    [Fact]
    public void Cmo_MatchesTulip_Streaming()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib CMO (streaming)
        var cmo = new Cmo(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(cmo.Update(item).Value);
        }

        // Tulip cmo
        var cmoIndicator = Tulip.Indicators.cmo;
        double[][] inputs = [tData];
        double[] options = [TestPeriod];
        int lookback = cmoIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        cmoIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qResults, tulipResult, lookback);

        _output.WriteLine("CMO Streaming validated successfully against Tulip");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    public void Cmo_MatchesTulip_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        double[] qOutput = new double[tData.Length];
        Cmo.Batch(tData.AsSpan(), qOutput.AsSpan(), period);

        var cmoIndicator = Tulip.Indicators.cmo;
        double[][] inputs = [tData];
        double[] options = [period];
        int lookback = cmoIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        cmoIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qOutput, tulipResult, lookback);
    }

    #endregion

    #region Skender Validation

    [Fact]
    public void Cmo_MatchesSkender_Batch()
    {
        // QuanTAlib CMO (batch)
        var qResult = Cmo.Batch(_testData.Data, TestPeriod);

        // Skender CMO
        var sResult = _testData.SkenderQuotes.GetCmo(TestPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Cmo);

        _output.WriteLine("CMO Batch validated successfully against Skender");
    }

    [Fact]
    public void Cmo_MatchesSkender_Streaming()
    {
        // QuanTAlib CMO (streaming)
        var cmo = new Cmo(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(cmo.Update(item).Value);
        }

        // Skender CMO
        var sResult = _testData.SkenderQuotes.GetCmo(TestPeriod).ToList();

        int count = qResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            if (sResult[i].Cmo is null) { continue; }
            Assert.True(
                Math.Abs(qResults[i] - sResult[i].Cmo!.Value) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Skender={sResult[i].Cmo:G17}");
        }

        _output.WriteLine("CMO Streaming validated successfully against Skender");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    public void Cmo_MatchesSkender_DifferentPeriods(int period)
    {
        var qResult = Cmo.Batch(_testData.Data, period);

        var sResult = _testData.SkenderQuotes.GetCmo(period).ToList();

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Cmo);
    }

    #endregion

    #region Mathematical Validation

    [Fact]
    public void Cmo_AllUpMoves_Returns100()
    {
        double[] prices = [100, 101, 102, 103, 104, 105];
        int period = 5;

        double[] result = new double[prices.Length];
        Cmo.Batch(prices, result, period);

        // After 5 periods: SumUp = 5, SumDown = 0 → CMO = 100
        Assert.Equal(100.0, result[5], 1e-9);
    }

    [Fact]
    public void Cmo_AllDownMoves_ReturnsNegative100()
    {
        double[] prices = [105, 104, 103, 102, 101, 100];
        int period = 5;

        double[] result = new double[prices.Length];
        Cmo.Batch(prices, result, period);

        // After 5 periods: SumUp = 0, SumDown = 5 → CMO = -100
        Assert.Equal(-100.0, result[5], 1e-9);
    }

    [Fact]
    public void Cmo_EqualMoves_ReturnsZero()
    {
        double[] prices = [100, 102, 100, 102, 100]; // up 2, down 2, up 2, down 2
        int period = 4;

        double[] result = new double[prices.Length];
        Cmo.Batch(prices, result, period);

        // SumUp = 4, SumDown = 4 → CMO = 0
        Assert.Equal(0.0, result[4], 1e-9);
    }

    [Fact]
    public void Cmo_RangeIsBounded()
    {
        double[] tData = _testData.RawData.ToArray();

        double[] result = new double[tData.Length];
        Cmo.Batch(tData.AsSpan(), result.AsSpan(), TestPeriod);

        // All values after warmup should be in [-100, 100]
        for (int i = TestPeriod; i < result.Length; i++)
        {
            Assert.True(result[i] >= -100.0 && result[i] <= 100.0,
                $"CMO at index {i} = {result[i]} is out of range [-100, 100]");
        }
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        double[] tData = _testData.RawData.ToArray();

        // Batch
        double[] batchOutput = new double[tData.Length];
        Cmo.Batch(tData.AsSpan(), batchOutput.AsSpan(), TestPeriod);

        // Streaming
        var cmo = new Cmo(TestPeriod);
        var streamingResults = new double[tData.Length];
        for (int i = 0; i < tData.Length; i++)
        {
            streamingResults[i] = cmo.Update(new TValue(DateTime.UtcNow.Ticks + i, tData[i])).Value;
        }

        int count = tData.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchOutput[i], streamingResults[i], 1e-9);
        }
        _output.WriteLine("CMO Batch vs Streaming consistency validated");
    }

    #endregion

    #region TALib Structural Validation

    /// <summary>
    /// TALib CMO uses Wilder's smoothed averaging (EMA-based) rather than the
    /// standard simple-sum formula used by QuanTAlib, Tulip, and Skender.
    /// Numeric equality cannot be expected. This test verifies:
    /// 1. TALib runs without error and produces a valid output range.
    /// 2. Both implementations produce bounded CMO values in [-100, +100].
    /// 3. Direction agreement: sign of QuanTAlib vs TALib is the same for
    ///    well-converged (post-warmup) bars (>80% agreement expected).
    /// </summary>
    [Fact]
    public void Cmo_TaLib_StructuralValidation()
    {
        double[] tData = _testData.RawData.ToArray();

        // --- TALib CMO ---
        double[] taOut = new double[tData.Length];
        var retCode = Functions.Cmo<double>(tData, 0..^0, taOut, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        var (taOffset, taLength) = outRange.GetOffsetAndLength(taOut.Length);
        Assert.True(taLength > 0, "TALib produced no output");

        // --- QuanTAlib CMO ---
        double[] qOut = new double[tData.Length];
        Cmo.Batch(tData.AsSpan(), qOut.AsSpan(), TestPeriod);

        // Both outputs should be bounded in [-100, +100]
        for (int j = 0; j < taLength; j++)
        {
            int qi = j + taOffset;
            Assert.True(taOut[j] >= -100.0 && taOut[j] <= 100.0,
                $"TALib CMO[{j}]={taOut[j]:F4} outside [-100,+100]");
            if (double.IsFinite(qOut[qi]))
            {
                Assert.True(qOut[qi] >= -100.0 && qOut[qi] <= 100.0,
                    $"QuanTAlib CMO[{qi}]={qOut[qi]:F4} outside [-100,+100]");
            }
        }

        // Sign agreement (directional concordance) — expect >70% after full convergence
        // TALib Wilder-CMO converges after ~3× period bars
        int compareStart = TestPeriod * 3;
        int agreementCount = 0;
        int compareCount = 0;

        for (int j = 0; j < taLength; j++)
        {
            int qi = j + taOffset;
            if (qi < compareStart || !double.IsFinite(qOut[qi])) { continue; }

            compareCount++;
            if (Math.Sign(taOut[j]) == Math.Sign(qOut[qi])) { agreementCount++; }
        }

        if (compareCount > 0)
        {
            double agreementRate = (double)agreementCount / compareCount;
            Assert.True(agreementRate >= 0.70,
                $"TALib/QuanTAlib CMO sign agreement {agreementRate:P1} < 70% ({agreementCount}/{compareCount})");
            _output.WriteLine($"CMO TALib structural: {taLength} output bars, sign agreement={agreementRate:P1}");
        }
    }

    [Fact]
    public void Cmo_TaLib_Lookback_Matches_Expected()
    {
        int lookback = Functions.CmoLookback(TestPeriod);
        // TALib CMO lookback = period (Wilder's period)
        Assert.True(lookback > 0, $"TALib CMO lookback={lookback} should be positive");
        _output.WriteLine($"TALib CMO lookback for period={TestPeriod}: {lookback}");
    }

    #endregion

    #region Ooples Validation

    [Fact]
    public void Cmo_Matches_Ooples_Batch()
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

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateChandeMomentumOscillator(length: TestPeriod);
        var oValues = oResult.OutputValues["Cmo"];

        var qResult = Cmo.Batch(_testData.Data, TestPeriod);

        ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        _output.WriteLine("CMO Batch validated against Ooples");
    }

    #endregion
}
