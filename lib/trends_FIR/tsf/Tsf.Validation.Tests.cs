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
}
