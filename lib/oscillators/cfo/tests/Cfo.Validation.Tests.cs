using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class CfoValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public CfoValidationTests(ITestOutputHelper output)
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
    public void Validate_Streaming_Batch_Span_Agree()
    {
        int period = 14;

        // Streaming
        var streaming = new Cfo(period);
        var streamValues = new List<double>(_testData.Data.Count);
        foreach (var item in _testData.Data)
        {
            streamValues.Add(streaming.Update(item).Value);
        }

        // Batch (TSeries)
        TSeries batchSeries = Cfo.Batch(_testData.Data, period);

        // Span
        double[] src = _testData.RawData.ToArray();
        double[] spanOutput = new double[src.Length];
        Cfo.Batch(src.AsSpan(), spanOutput.AsSpan(), period);

        // O(1) streaming sumXY maintenance accumulates cancellation drift vs full-recalc batch.
        // ResyncInterval=1000 bounds drift, but between resyncs tolerance must be relaxed.
        // Batch vs span should match exactly (same code path).
        int start = Math.Max(0, src.Length - 200);
        for (int i = start; i < src.Length; i++)
        {
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 12);   // batch≡span (same path)
            Assert.Equal(batchSeries[i].Value, streamValues[i], 4);   // streaming drifts ~1e-5 between resyncs
        }

        _output.WriteLine("CFO validation: streaming, batch, and span outputs agree within tolerance.");
    }

    [Fact]
    public void Validate_Against_LinReg()
    {
        // Cross-validate CFO against our own LinReg class.
        // LinReg.Last.Value = intercept = regression value at x=0 (current bar) = TSF.
        // CFO = 100 * (source - TSF) / source.
        int[] periods = [5, 10, 14, 20, 50];

        foreach (int period in periods)
        {
            var cfo = new Cfo(period);
            var linreg = new LinReg(period);

            int validCount = 0;

            foreach (var item in _testData.Data)
            {
                cfo.Update(item);
                linreg.Update(item);

                if (!cfo.IsHot || !linreg.IsHot)
                {
                    continue;
                }

                double src = item.Value;
                if (src == 0.0)
                {
                    continue;
                }

                double tsf = linreg.Last.Value; // intercept = regression at current bar
                double expectedCfo = 100.0 * (src - tsf) / src;
                double actualCfo = cfo.Last.Value;

                // skipcq: CS-R1140 - Absolute tolerance needed: two independent O(1) streaming implementations accumulate floating-point drift
                Assert.True(Math.Abs(expectedCfo - actualCfo) < 1e-6,
                    $"CFO mismatch at period={period}: expected={expectedCfo}, actual={actualCfo}, diff={Math.Abs(expectedCfo - actualCfo)}");
                validCount++;
            }

            Assert.True(validCount > 0, $"No valid comparison points for period {period}");
            _output.WriteLine($"CFO period={period}: validated {validCount} points against LinReg.");
        }
    }

    [Fact]
    public void Validate_KnownValues_LinearTrend()
    {
        // For a perfect linear trend y = a + b*x, the regression line exactly fits.
        // TSF should equal the source value, so CFO should be 0.
        int period = 5;
        var cfo = new Cfo(period);

        // Feed a perfect linear trend: 10, 11, 12, 13, 14, 15, ...
        for (int i = 0; i < 20; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        // After warmup, CFO should be ~0 for a perfect linear trend
        Assert.Equal(0.0, cfo.Last.Value, 10);
        _output.WriteLine("CFO known-values: perfect linear trend produces CFO=0.");
    }

    [Fact]
    public void Validate_MultiPeriod_Consistency()
    {
        // Different periods should produce different results
        int[] periods = [5, 14, 50];
        var results = new List<TSeries>();

        foreach (int period in periods)
        {
            results.Add(Cfo.Batch(_testData.Data, period));
        }

        // After all warmups, values should differ for different periods
        int checkIdx = 100;
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.NotEqual(results[i][checkIdx].Value, results[i + 1][checkIdx].Value);
        }

        _output.WriteLine("CFO multi-period: different periods produce different results.");
    }
}
