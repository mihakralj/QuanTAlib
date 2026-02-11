using Skender.Stock.Indicators;
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
}
