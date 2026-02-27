using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for MOM (Momentum) against external libraries.
/// MOM = Price - Price[N] (absolute change)
///
/// TALib's Mom and Tulip's mom both compute the same absolute change.
/// Skender's GetRoc returns RocResult with .Momentum property (absolute change).
/// </summary>
public sealed class MomValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 10;

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

    #region TALib Validation

    [Fact]
    public void Mom_MatchesTalib_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib MOM (batch TSeries)
        var qResult = Mom.Batch(_testData.Data, TestPeriod);

        // TALib Mom
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Mom<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MomLookback(TestPeriod);

        int count = qResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback) { continue; }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) { continue; }

            Assert.True(
                Math.Abs(qResult[i].Value - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResult[i].Value:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine("MOM Batch validated successfully against TALib");
    }

    [Fact]
    public void Mom_MatchesTalib_Span()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib MOM (Span)
        double[] qOutput = new double[tData.Length];
        Mom.Batch(tData.AsSpan(), qOutput.AsSpan(), TestPeriod);

        // TALib Mom
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Mom<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MomLookback(TestPeriod);

        ValidationHelper.VerifyData(qOutput, tOutput, outRange, lookback);

        _output.WriteLine("MOM Span validated successfully against TALib");
    }

    [Fact]
    public void Mom_MatchesTalib_Streaming()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib MOM (streaming)
        var mom = new Mom(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(mom.Update(item).Value);
        }

        // TALib Mom
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Mom<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MomLookback(TestPeriod);

        ValidationHelper.VerifyData(qResults, tOutput, outRange, lookback);

        _output.WriteLine("MOM Streaming validated successfully against TALib");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(50)]
    public void Mom_MatchesTalib_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        var qResult = Mom.Batch(_testData.Data, period);

        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Mom<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MomLookback(period);

        int count = qResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback) { continue; }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) { continue; }

            Assert.True(
                Math.Abs(qResult[i].Value - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Period {period}, index {i}: QuanTAlib={qResult[i].Value:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine($"MOM period={period} validated against TALib");
    }

    #endregion

    #region Tulip Validation

    [Fact]
    public void Mom_MatchesTulip_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        var qResult = Mom.Batch(_testData.Data, TestPeriod);

        // Tulip mom
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tData];
        double[] options = [TestPeriod];
        int lookback = momIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qResult, tulipResult, lookback);

        _output.WriteLine("MOM Batch validated successfully against Tulip");
    }

    [Fact]
    public void Mom_MatchesTulip_Streaming()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib MOM (streaming)
        var mom = new Mom(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(mom.Update(item).Value);
        }

        // Tulip mom
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tData];
        double[] options = [TestPeriod];
        int lookback = momIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qResults, tulipResult, lookback);

        _output.WriteLine("MOM Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Mom_MatchesTulip_Span()
    {
        double[] tData = _testData.RawData.ToArray();

        double[] qOutput = new double[tData.Length];
        Mom.Batch(tData.AsSpan(), qOutput.AsSpan(), TestPeriod);

        // Tulip mom
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tData];
        double[] options = [TestPeriod];
        int lookback = momIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qOutput, tulipResult, lookback);

        _output.WriteLine("MOM Span validated successfully against Tulip");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(50)]
    public void Mom_MatchesTulip_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        var qResult = Mom.Batch(_testData.Data, period);

        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tData];
        double[] options = [period];
        int lookback = momIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qResult, tulipResult, lookback);
    }

    #endregion

    #region Skender Validation

    [Fact]
    public void Mom_MatchesSkender_Batch()
    {
        // QuanTAlib MOM
        var qResult = Mom.Batch(_testData.Data, TestPeriod);

        // Skender GetRoc returns RocResult with .Momentum (absolute change = current - past)
        var sResult = _testData.SkenderQuotes.GetRoc(TestPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Momentum);

        _output.WriteLine("MOM Batch validated successfully against Skender (GetRoc.Momentum)");
    }

    [Fact]
    public void Mom_MatchesSkender_Streaming()
    {
        // QuanTAlib MOM (streaming)
        var mom = new Mom(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(mom.Update(item).Value);
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

        _output.WriteLine("MOM Streaming validated successfully against Skender (GetRoc.Momentum)");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(50)]
    public void Mom_MatchesSkender_DifferentPeriods(int period)
    {
        var qResult = Mom.Batch(_testData.Data, period);

        var sResult = _testData.SkenderQuotes.GetRoc(period).ToList();

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Momentum);
    }

    #endregion

    #region Mathematical Validation

    [Fact]
    public void Mom_ManualCalculation_MatchesExpected()
    {
        var mom = new Mom(3);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 115, 120, 125 };

        for (int i = 0; i < values.Length; i++)
        {
            var result = mom.Update(new TValue(time.AddSeconds(i), values[i]), true);

            if (i >= 3)
            {
                double expected = values[i] - values[i - 3];
                Assert.Equal(expected, result.Value, 10);
            }
        }
    }

    [Fact]
    public void Mom_ConstantValues_ReturnsZero()
    {
        var constantData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            constantData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        var result = Mom.Batch(constantData, TestPeriod);

        for (int i = TestPeriod; i < 100; i++)
        {
            Assert.Equal(0.0, result[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Mom_LinearIncrease_ReturnsConstant()
    {
        var linearData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            linearData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        var result = Mom.Batch(linearData, TestPeriod);

        // Linear increase by 1 per bar → MOM = period after warmup
        for (int i = TestPeriod; i < 100; i++)
        {
            Assert.Equal(TestPeriod, result[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        var source = _testData.Data;

        // Streaming
        var streamingMom = new Mom(TestPeriod);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults.Add(streamingMom.Update(source[i]).Value);
        }

        // Batch
        var batchResult = Mom.Batch(source, TestPeriod);

        int count = source.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("MOM Batch vs Streaming consistency validated");
    }

    #endregion

    [Fact]
    public void Mom_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateMomentumOscillator();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}