using Skender.Stock.Indicators;
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
}
