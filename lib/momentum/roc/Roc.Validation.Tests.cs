using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ROC (Rate of Change) against external libraries.
/// ROC computes absolute change: current - past (same as momentum).
///
/// Tulip's MOM calculates absolute change: current - past.
/// Skender's GetRoc returns RocResult with .Momentum (absolute change).
/// </summary>
public sealed class RocValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 9;
    private const double TulipTolerance = 1e-9;

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

    #region Tulip MOM Validation

    [Fact]
    public void Roc_MatchesTulipMom_Batch()
    {
        double[] tulipInput = _testData.RawData.ToArray();

        // Get QuanTAlib ROC result
        var quantResult = Roc.Batch(_testData.Data, TestPeriod);

        // Calculate Tulip MOM (momentum = current - past)
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [TestPeriod];
        int lookback = TestPeriod;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        ValidationHelper.VerifyData(quantResult, tulipResult, lookback);

        _output.WriteLine("ROC Batch validated successfully against Tulip MOM");
    }

    [Fact]
    public void Roc_MatchesTulipMom_Streaming()
    {
        double[] tulipInput = _testData.RawData.ToArray();

        // Get QuanTAlib ROC result via streaming
        var roc = new Roc(TestPeriod);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            streamingResults.Add(roc.Update(item).Value);
        }

        // Calculate Tulip MOM
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [TestPeriod];
        int lookback = TestPeriod;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        ValidationHelper.VerifyData(streamingResults, tulipResult, lookback);

        _output.WriteLine("ROC Streaming validated successfully against Tulip MOM");
    }

    [Fact]
    public void Roc_MatchesTulipMom_Span()
    {
        double[] tulipInput = _testData.RawData.ToArray();

        // Get QuanTAlib ROC result via span
        var quantOutput = new double[tulipInput.Length];
        Roc.Batch(new ReadOnlySpan<double>(tulipInput), quantOutput, TestPeriod);

        // Calculate Tulip MOM
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [TestPeriod];
        int lookback = TestPeriod;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        ValidationHelper.VerifyData(quantOutput, tulipResult, lookback);

        _output.WriteLine("ROC Span validated successfully against Tulip MOM");
    }

    #endregion

    #region Different Periods

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Roc_MatchesTulipMom_DifferentPeriods(int period)
    {
        double[] tulipInput = _testData.RawData.ToArray();

        var quantResult = Roc.Batch(_testData.Data, period);

        // Calculate Tulip MOM
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [period];
        int lookback = period;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        ValidationHelper.VerifyData(quantResult, tulipResult, lookback);
    }

    #endregion

    #region Skender Validation

    [Fact]
    public void Roc_MatchesSkender_Batch()
    {
        // QuanTAlib ROC
        var qResult = Roc.Batch(_testData.Data, TestPeriod);

        // Skender GetRoc returns RocResult with .Momentum (absolute change)
        var sResult = _testData.SkenderQuotes.GetRoc(TestPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Momentum);

        _output.WriteLine("ROC Batch validated successfully against Skender (GetRoc.Momentum)");
    }

    [Fact]
    public void Roc_MatchesSkender_Streaming()
    {
        // QuanTAlib ROC (streaming)
        var roc = new Roc(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(roc.Update(item).Value);
        }

        // Skender GetRoc
        var sResult = _testData.SkenderQuotes.GetRoc(TestPeriod).ToList();

        int count = qResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            if (sResult[i].Momentum is null) { continue; }
            Assert.True(
                Math.Abs(qResults[i] - sResult[i].Momentum!.Value) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Skender={sResult[i].Momentum:G17}");
        }

        _output.WriteLine("ROC Streaming validated successfully against Skender (GetRoc.Momentum)");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(50)]
    public void Roc_MatchesSkender_DifferentPeriods(int period)
    {
        var qResult = Roc.Batch(_testData.Data, period);

        var sResult = _testData.SkenderQuotes.GetRoc(period).ToList();

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Momentum);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Roc_HandlesConstantValues()
    {
        var constantData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            constantData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        var result = Roc.Batch(constantData, TestPeriod);

        // Constant values should produce 0 change after warmup
        for (int i = TestPeriod; i < 100; i++)
        {
            Assert.Equal(0.0, result[i].Value, TulipTolerance);
        }
    }

    [Fact]
    public void Roc_HandlesLinearlyIncreasing()
    {
        var linearData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            linearData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        var result = Roc.Batch(linearData, TestPeriod);

        // Linear increase by 1 per bar means ROC = period after warmup
        for (int i = TestPeriod; i < 100; i++)
        {
            Assert.Equal(TestPeriod, result[i].Value, TulipTolerance);
        }
    }

    [Fact]
    public void Roc_Period1_MatchesTulipMom()
    {
        double[] tulipInput = _testData.RawData.ToArray();

        var quantResult = Roc.Batch(_testData.Data, 1);

        // Calculate Tulip MOM with period 1
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [1];
        int lookback = 1;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        ValidationHelper.VerifyData(quantResult, tulipResult, lookback);

        _output.WriteLine("ROC Period=1 validated against Tulip MOM");
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        // Batch
        var batchResult = Roc.Batch(_testData.Data, TestPeriod);

        // Streaming
        var roc = new Roc(TestPeriod);
        var streamingResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamingResults.Add(roc.Update(item).Value);
        }

        int count = _testData.Data.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("ROC Batch vs Streaming consistency validated");
    }

    #endregion

    #region TALib Validation

    /// <summary>
    /// TALib MOM = price - prevPrice (absolute momentum), which is exactly what
    /// QuanTAlib ROC computes. TALib ROC = ((price/prevPrice)-1)*100 (percentage) — different.
    /// So we validate QuanTAlib ROC against TALib MOM (not TALib ROC).
    /// </summary>
    [Fact]
    public void Roc_MatchesTalib_Mom_Span()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROC via Span
        double[] qOutput = new double[tData.Length];
        Roc.Batch(new ReadOnlySpan<double>(tData), qOutput, TestPeriod);

        // TALib MOM (absolute momentum = price - prevPrice)
        double[] taOut = new double[tData.Length];
        var retCode = Functions.Mom<double>(tData, 0..^0, taOut, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = Functions.MomLookback(TestPeriod);
        ValidationHelper.VerifyData(qOutput, taOut, outRange, lookback);

        _output.WriteLine($"ROC (absolute) Span validated against TALib MOM (period={TestPeriod})");
    }

    [Fact]
    public void Roc_MatchesTalib_Mom_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROC via streaming
        var roc = new Roc(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(roc.Update(item).Value);
        }

        // TALib MOM
        double[] taOut = new double[tData.Length];
        var retCode = Functions.Mom<double>(tData, 0..^0, taOut, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = Functions.MomLookback(TestPeriod);
        ValidationHelper.VerifyData(qResults, taOut, outRange, lookback);

        _output.WriteLine($"ROC (absolute) Streaming validated against TALib MOM (period={TestPeriod})");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Roc_MatchesTalib_Mom_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        double[] qOutput = new double[tData.Length];
        Roc.Batch(new ReadOnlySpan<double>(tData), qOutput, period);

        double[] taOut = new double[tData.Length];
        var retCode = Functions.Mom<double>(tData, 0..^0, taOut, out var outRange, period);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = Functions.MomLookback(period);
        ValidationHelper.VerifyData(qOutput, taOut, outRange, lookback);
    }

    #endregion

    #region Ooples Validation

    /// <summary>
    /// Ooples ROC = percentage change: (close - prevClose) / prevClose * 100.
    /// QuanTAlib ROC = absolute change: close - prevClose.
    /// These are different formulas. Structural: both produce finite output, values differ.
    /// </summary>
    [Fact]
    public void Roc_Ooples_StructuralVariant_BothFinite()
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
        var oResult = stockData.CalculateRateOfChange(length: TestPeriod);
        var oValues = oResult.OutputValues.Values.First();

        // QuanTAlib ROC (absolute)
        double[] qOutput = new double[_testData.RawData.Length];
        Roc.Batch(_testData.RawData.Span, qOutput.AsSpan(), TestPeriod);

        // Structural: Ooples ROC is percentage (not absolute), both must be finite after warmup
        Assert.True(oValues.Count > 0, "Ooples ROC must produce output");
        int finiteCount = 0;
        for (int i = TestPeriod; i < oValues.Count; i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qOutput[i]))
            {
                finiteCount++;
            }
        }

        Assert.True(finiteCount > 100, $"Expected >100 finite pairs, got {finiteCount}");
        _output.WriteLine($"ROC Ooples structural: Ooples=percentage, QuanTAlib=absolute. {finiteCount} finite pairs verified.");
    }

    #endregion
}
