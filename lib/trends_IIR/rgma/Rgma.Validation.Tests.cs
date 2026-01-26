using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for RGMA (Recursive Gaussian Moving Average).
/// Validates internal consistency across modes and checks the degenerate case:
/// passes=1 reduces to EMA with alpha = 2/(period+1).
/// </summary>
public sealed class RgmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public RgmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
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
    public void Validate_Passes1_MatchesEma_Batch()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var rgma = new Rgma(period, passes: 1);
            var ema = new Ema(period);

            var rgmaResult = rgma.Update(_testData.Data);
            var emaResult = ema.Update(_testData.Data);

            int compareCount = Math.Min(200, rgmaResult.Count);
            int startIdx = rgmaResult.Count - compareCount;

            for (int i = startIdx; i < rgmaResult.Count; i++)
            {
                Assert.Equal(emaResult[i].Value, rgmaResult[i].Value, 1e-10);
            }
        }

        _output.WriteLine("RGMA(passes=1) Batch validated successfully against EMA");
    }

    [Fact]
    public void Validate_Passes1_MatchesEma_Streaming()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var rgma = new Rgma(period, passes: 1);
            var ema = new Ema(period);

            var rgmaResults = new List<double>();
            var emaResults = new List<double>();

            foreach (var item in _testData.Data)
            {
                rgmaResults.Add(rgma.Update(item).Value);
                emaResults.Add(ema.Update(item).Value);
            }

            int compareCount = Math.Min(200, rgmaResults.Count);
            int startIdx = rgmaResults.Count - compareCount;

            for (int i = startIdx; i < rgmaResults.Count; i++)
            {
                Assert.Equal(emaResults[i], rgmaResults[i], 1e-10);
            }
        }

        _output.WriteLine("RGMA(passes=1) Streaming validated successfully against EMA");
    }

    [Fact]
    public void Validate_Passes1_MatchesEma_Span()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] rgmaOutput = new double[sourceData.Length];
            double[] emaOutput = new double[sourceData.Length];

            Rgma.Batch(sourceData.AsSpan(), rgmaOutput.AsSpan(), period, passes: 1);
            Ema.Batch(sourceData.AsSpan(), emaOutput.AsSpan(), period);

            int compareCount = Math.Min(200, sourceData.Length);
            int startIdx = sourceData.Length - compareCount;

            for (int i = startIdx; i < sourceData.Length; i++)
            {
                Assert.Equal(emaOutput[i], rgmaOutput[i], 1e-10);
            }
        }

        _output.WriteLine("RGMA(passes=1) Span validated successfully against EMA");
    }

    [Fact]
    public void Validate_BatchStreamingSpan_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };
        int[] passes = { 1, 2, 3, 5 };

        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            foreach (var passCount in passes)
            {
                // Batch (TSeries)
                var rgmaBatch = new Rgma(period, passCount);
                var batchResult = rgmaBatch.Update(_testData.Data);

                // Streaming
                var rgmaStream = new Rgma(period, passCount);
                var streaming = new double[_testData.Data.Count];
                for (int i = 0; i < _testData.Data.Count; i++)
                {
                    streaming[i] = rgmaStream.Update(_testData.Data[i]).Value;
                }

                // Span
                var spanOutput = new double[sourceData.Length];
                Rgma.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period, passCount);

                for (int i = 0; i < batchResult.Count; i++)
                {
                    Assert.Equal(batchResult[i].Value, streaming[i], 1e-10);
                    Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-10);
                }
            }
        }

        _output.WriteLine("RGMA Batch/Streaming/Span consistency validated successfully");
    }
}

