using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class RmedValidationTests : IDisposable
{
    private readonly GBM _gbm = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public RmedValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
        }
    }

    [Fact]
    public void Rmed_SelfConsistency_StreamingMatchesBatch()
    {
        const int period = 12;
        TSeries src = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Streaming
        var rmed = new Rmed(period);
        double[] streaming = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streaming[i] = rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true).Value;
        }

        // Batch
        double[] batch = new double[src.Count];
        Rmed.Batch(src.Values, batch.AsSpan(), period);

        double maxDiff = 0;
        for (int i = 0; i < src.Count; i++)
        {
            double diff = Math.Abs(streaming[i] - batch[i]);
            maxDiff = Math.Max(maxDiff, diff);
        }

        _output.WriteLine($"Max streaming vs batch diff: {maxDiff:E3}");
        Assert.True(maxDiff < 1e-10, $"Max diff {maxDiff} exceeds tolerance");
    }

    [Fact]
    public void Rmed_SelfConsistency_TSeriesBatchMatchesSpanBatch()
    {
        const int period = 10;
        TSeries src = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        TSeries tsBatch = Rmed.Batch(src, period);

        double[] spanBatch = new double[src.Count];
        Rmed.Batch(src.Values, spanBatch.AsSpan(), period);

        double maxDiff = 0;
        for (int i = 0; i < src.Count; i++)
        {
            double diff = Math.Abs(tsBatch.Values[i] - spanBatch[i]);
            maxDiff = Math.Max(maxDiff, diff);
        }

        _output.WriteLine($"Max TSeries vs Span batch diff: {maxDiff:E3}");
        Assert.True(maxDiff < 1e-10, $"Max diff {maxDiff} exceeds tolerance");
    }

    [Fact]
    public void Rmed_SelfConsistency_CalculateMatchesBatch()
    {
        const int period = 12;
        TSeries src = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        var (calcResult, indicator) = Rmed.Calculate(src, period);
        TSeries batchResult = Rmed.Batch(src, period);

        double maxDiff = 0;
        for (int i = 0; i < src.Count; i++)
        {
            double diff = Math.Abs(calcResult.Values[i] - batchResult.Values[i]);
            maxDiff = Math.Max(maxDiff, diff);
        }

        _output.WriteLine($"Max Calculate vs Batch diff: {maxDiff:E3}");
        Assert.True(maxDiff < 1e-10, $"Max diff {maxDiff} exceeds tolerance");
        Assert.Equal(period, indicator.Period);
    }

    [Fact]
    public void Rmed_SpikeRejection_Property()
    {
        // Verify that the median stage truly rejects a single spike
        const int period = 12;
        TSeries src = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Clean run
        double[] clean = new double[src.Count];
        Rmed.Batch(src.Values, clean.AsSpan(), period);

        // Run with single spike at bar 25
        double[] spiked = new double[src.Count];
        src.Values.CopyTo(spiked);
        spiked[25] = src.Values[25] + 100000.0; // massive spike

        double[] spikedOut = new double[src.Count];
        Rmed.Batch(spiked.AsSpan(), spikedOut.AsSpan(), period);

        // The spike at bar 25 should barely affect the output at bar 25+1
        // because median of 5 rejects a single outlier
        double cleanVal = clean[26];
        double spikedVal = spikedOut[26];
        double impact = Math.Abs(spikedVal - cleanVal);
        double relativeImpact = impact / Math.Abs(cleanVal);

        _output.WriteLine($"Clean[26]={cleanVal:F6}, Spiked[26]={spikedVal:F6}, Impact={impact:F6}, Relative={relativeImpact:P2}");

        // The spike should have minimal impact (< 50% relative change)
        Assert.True(relativeImpact < 0.50, $"Spike impact {relativeImpact:P2} exceeds 50% — median rejection failed");
    }

    [Fact]
    public void Rmed_EhlersAlpha_MatchesKnownValues()
    {
        // Verify alpha = (cos θ + sin θ - 1) / cos θ where θ = 2π/P
        // P=5 → α≈0.8416, P=10 → α≈0.4905, P=20 → α≈0.2735, P=40 → α≈0.1459
        var rmed5 = new Rmed(5);
        var rmed10 = new Rmed(10);
        var rmed20 = new Rmed(20);
        var rmed40 = new Rmed(40);

        _output.WriteLine($"P=5: α={rmed5.Alpha:F4}");
        _output.WriteLine($"P=10: α={rmed10.Alpha:F4}");
        _output.WriteLine($"P=20: α={rmed20.Alpha:F4}");
        _output.WriteLine($"P=40: α={rmed40.Alpha:F4}");

        Assert.InRange(rmed5.Alpha, 0.80, 0.90);
        Assert.InRange(rmed10.Alpha, 0.44, 0.55);
        Assert.InRange(rmed20.Alpha, 0.24, 0.32);
        Assert.InRange(rmed40.Alpha, 0.12, 0.18);
    }

    [Fact]
    public void Rmed_DifferentPeriods_HaveDifferentSmoothness()
    {
        TSeries src = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        double[] out5 = new double[src.Count];
        double[] out40 = new double[src.Count];
        Rmed.Batch(src.Values, out5.AsSpan(), 5);
        Rmed.Batch(src.Values, out40.AsSpan(), 40);

        // Compute roughness (sum of absolute first differences)
        double roughness5 = 0, roughness40 = 0;
        for (int i = 1; i < src.Count; i++)
        {
            roughness5 += Math.Abs(out5[i] - out5[i - 1]);
            roughness40 += Math.Abs(out40[i] - out40[i - 1]);
        }

        _output.WriteLine($"Roughness P=5: {roughness5:F2}, P=40: {roughness40:F2}");

        // Longer period → smoother → less roughness
        Assert.True(roughness40 < roughness5, "Longer period should produce smoother output");
    }

    [Fact]
    public void Rmed_BarCorrection_IsReversible()
    {
        const int period = 12;
        TSeries src = _gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        var rmed = new Rmed(period);
        for (int i = 0; i < src.Count - 1; i++)
        {
            rmed.Update(new TValue(src.Times[i], src.Values[i]), isNew: true);
        }

        // Process last bar
        var originalLast = rmed.Update(new TValue(src.Times[^1], src.Values[^1]), isNew: true);

        // Correct it with different value
        rmed.Update(new TValue(src.Times[^1], src.Values[^1] + 50.0), isNew: false);

        // Correct it back to original
        var restored = rmed.Update(new TValue(src.Times[^1], src.Values[^1]), isNew: false);

        _output.WriteLine($"Original: {originalLast.Value:F10}, Restored: {restored.Value:F10}");
        Assert.Equal(originalLast.Value, restored.Value, 10);
    }
}
