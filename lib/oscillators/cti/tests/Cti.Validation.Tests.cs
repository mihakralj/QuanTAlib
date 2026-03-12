using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class CtiValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public CtiValidationTests(ITestOutputHelper output)
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
        int period = 20;

        // Streaming
        var streaming = new Cti(period);
        var streamValues = new List<double>(_testData.Data.Count);
        foreach (var item in _testData.Data)
        {
            streamValues.Add(streaming.Update(item).Value);
        }

        // Batch (TSeries)
        TSeries batchSeries = Cti.Batch(_testData.Data, period);

        // Span
        double[] src = _testData.RawData.ToArray();
        double[] spanOutput = new double[src.Length];
        Cti.Batch(src.AsSpan(), spanOutput.AsSpan(), period);

        // Batch and span should be identical (same code path through RingBuffer)
        // Streaming uses O(1) incremental updates with ResyncInterval=1000
        int start = Math.Max(0, src.Length - 200);
        for (int i = start; i < src.Length; i++)
        {
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 12);
            Assert.Equal(batchSeries[i].Value, streamValues[i], 4);
        }

        _output.WriteLine("CTI validation: streaming, batch, and span outputs agree within tolerance.");
    }

    [Fact]
    public void Validate_PerfectCorrelation_Ascending()
    {
        // Arithmetic sequence: each element is exactly i+1
        // Expected: Pearson r = 1.0 exactly (perfect positive linear correlation)
        int period = 15;
        var cti = new Cti(period);

        double lastValue = 0.0;
        for (int i = 1; i <= 50; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, i * 1.0));
            if (cti.IsHot)
            {
                lastValue = cti.Last.Value;
            }
        }

        Assert.Equal(1.0, lastValue, 10);
        _output.WriteLine($"CTI ascending sequence: {lastValue:F15}");
    }

    [Fact]
    public void Validate_PerfectCorrelation_Descending()
    {
        // Descending arithmetic sequence → perfect negative correlation → CTI = -1.0
        int period = 15;
        var cti = new Cti(period);

        double lastValue = 0.0;
        for (int i = 50; i >= 1; i--)
        {
            cti.Update(new TValue(DateTime.UtcNow, i * 1.0));
            if (cti.IsHot)
            {
                lastValue = cti.Last.Value;
            }
        }

        Assert.Equal(-1.0, lastValue, 10);
        _output.WriteLine($"CTI descending sequence: {lastValue:F15}");
    }

    [Fact]
    public void Validate_ConstantInput_ReturnsZero()
    {
        // Constant price: variance = 0 → denomY = 0 → return 0
        int period = 10;
        var cti = new Cti(period);

        for (int i = 0; i < 30; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        Assert.True(cti.IsHot);
        Assert.Equal(0.0, cti.Last.Value, 10);
    }

    [Fact]
    public void Validate_Output_AlwaysBounded()
    {
        // With random GBM data, output must stay in [-1, +1]
        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.5, seed: 999);
        var bars = gbm.Fetch(2000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (int period in new[] { 5, 10, 20, 50, 100 })
        {
            TSeries batch = Cti.Batch(bars.Close, period);
            foreach (var tv in batch)
            {
                Assert.InRange(tv.Value, -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void Validate_Batch_Calculate_Agree()
    {
        int period = 14;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.2, seed: 77);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries batchResult = Cti.Batch(source, period);
        var (calcResult, _) = Cti.Calculate(source, period);

        for (int i = period; i < source.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, calcResult[i].Value, 10);
        }
    }

    [Fact]
    public void Validate_BarCorrection_Consistency()
    {
        // After bar correction restores original value, result must equal baseline
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 31);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var cti = new Cti(period);
        for (int i = 0; i < source.Count - 1; i++)
        {
            cti.Update(source[i], isNew: true);
        }

        // Final bar
        cti.Update(source[^1], isNew: true);
        double baseline = cti.Last.Value;

        // Correct and revert
        cti.Update(new TValue(source[^1].Time, 99999.0), isNew: false);
        cti.Update(new TValue(source[^1].Time, source[^1].Value), isNew: false);

        Assert.Equal(baseline, cti.Last.Value, 7);
    }

    [Fact]
    public void Validate_DifferentPeriods_Produce_Different_Results()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.2, seed: 55);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries r5 = Cti.Batch(source, 5);
        TSeries r20 = Cti.Batch(source, 20);
        TSeries r50 = Cti.Batch(source, 50);

        // Different periods should generally not produce identical results
        double sum5 = 0, sum20 = 0, sum50 = 0;
        for (int i = 50; i < source.Count; i++)
        {
            sum5 += r5[i].Value;
            sum20 += r20[i].Value;
            sum50 += r50[i].Value;
        }

        // Sums at different periods should differ
        Assert.NotEqual(sum5, sum20);
        Assert.NotEqual(sum20, sum50);
    }

    [Fact]
    public void Validate_Reset_Reprocess_Deterministic()
    {
        int period = 15;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.2, seed: 13);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var cti = new Cti(period);
        double[] first = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            first[i] = cti.Update(source[i]).Value;
        }

        cti.Reset();
        double[] second = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            second[i] = cti.Update(source[i]).Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(first[i], second[i], 15);
        }
    }
}
