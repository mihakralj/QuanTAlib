namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation tests for the EDCF indicator.
/// No external library implements EDCF, so validation uses:
/// - All-modes consistency (streaming == batch == span)
/// - SMA degeneracy (flat prices → SMA behavior)
/// - Constant convergence (constant input → constant output)
/// - Smoothing behavior (longer length = smoother output)
/// - Determinism (identical inputs → identical outputs)
/// - Mathematical properties (weighted average bounds)
/// </summary>
public class EdcfValidationTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void AllModes_AreConsistent()
    {
        int length = 7;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Streaming
        var streaming = new Edcf(length);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Batch
        var batchResults = Edcf.Batch(series, length);

        // Span
        double[] srcVals = series.Values.ToArray();
        double[] spanResults = new double[series.Count];
        Edcf.Batch(srcVals.AsSpan(), spanResults.AsSpan(), length);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, Tolerance);
            Assert.Equal(streamResults[i], spanResults[i], Tolerance);
        }
    }

    [Fact]
    public void BatchAndStreaming_Match()
    {
        int length = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: 77);
        var data = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Streaming
        var streaming = new Edcf(length);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Batch
        var batchResults = Edcf.Batch(series, length);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, Tolerance);
        }
    }

    [Fact]
    public void ConstantInput_ProducesConstantOutput()
    {
        double constVal = 77.5;
        var ind = new Edcf(10);
        for (int i = 0; i < 50; i++)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constVal));
            // After first bar, output should always be the constant
            Assert.Equal(constVal, result.Value, Tolerance);
        }
    }

    [Fact]
    public void LongerLength_SmoothsMore()
    {
        // Longer length should produce smoother output (lower bar-to-bar change variance)
        var gbm = new GBM(startPrice: 100, mu: 0.1, sigma: 0.3, seed: 99);
        var data = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var shortEdcf = new Edcf(3);
        var longEdcf = new Edcf(15);

        var shortResults = new double[series.Count];
        var longResults = new double[series.Count];

        for (int i = 0; i < series.Count; i++)
        {
            shortResults[i] = shortEdcf.Update(series[i]).Value;
            longResults[i] = longEdcf.Update(series[i]).Value;
        }

        // Compute first-difference variance (smoothness measure)
        // Smoother signal = lower first-difference variance
        int start = 20; // skip warmup
        double shortDiffVar = 0, longDiffVar = 0;
        int n = series.Count - start - 1;
        for (int i = start; i < series.Count - 1; i++)
        {
            double sd = shortResults[i + 1] - shortResults[i];
            shortDiffVar += sd * sd;
            double ld = longResults[i + 1] - longResults[i];
            longDiffVar += ld * ld;
        }
        shortDiffVar /= n;
        longDiffVar /= n;

        // Longer filter should have lower first-difference variance (smoother)
        Assert.True(longDiffVar < shortDiffVar,
            $"Long diff-var {longDiffVar} should be less than short diff-var {shortDiffVar}");
    }

    [Fact]
    public void Determinism_IdenticalInputs_IdenticalOutputs()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);
        var data = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var ind1 = new Edcf(8);
        var ind2 = new Edcf(8);

        for (int i = 0; i < series.Count; i++)
        {
            var r1 = ind1.Update(series[i]);
            var r2 = ind2.Update(series[i]);
            Assert.Equal(r1.Value, r2.Value, Tolerance);
        }
    }

    [Fact]
    public void Output_BoundedByInputRange()
    {
        // EDCF is a weighted average → output must be within input range (once warm)
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.15, seed: 33);
        var data = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        int length = 5;

        var ind = new Edcf(length);
        for (int i = 0; i < series.Count; i++)
        {
            var result = ind.Update(series[i]);
            if (i >= length)
            {
                // Find min/max of the last 'length' source values
                double min = double.MaxValue, max = double.MinValue;
                for (int j = Math.Max(0, i - length + 1); j <= i; j++)
                {
                    if (series[j].Value < min) { min = series[j].Value; }
                    if (series[j].Value > max) { max = series[j].Value; }
                }
                // Allow small tolerance for floating-point
                Assert.True(result.Value >= min - 1e-6 && result.Value <= max + 1e-6,
                    $"EDCF {result.Value} outside [{min}, {max}] at bar {i}");
            }
        }
    }

    [Fact]
    public void NaN_Input_NeverPropagates()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 11);
        var data = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        var ind = new Edcf(5);

        for (int i = 0; i < series.Count; i++)
        {
            double val = (i % 7 == 3) ? double.NaN : series[i].Value;
            var result = ind.Update(new TValue(series[i].Time, val));
            Assert.True(double.IsFinite(result.Value), $"NaN propagated at bar {i}");
        }
    }

    [Fact]
    public void LargeDataset_Stable()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.1, seed: 22);
        var data = gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        var ind = new Edcf(15);

        for (int i = 0; i < series.Count; i++)
        {
            var result = ind.Update(series[i]);
            Assert.True(double.IsFinite(result.Value), $"Non-finite at bar {i}");
        }
    }

    [Fact]
    public void DifferentLengths_AllValid()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 44);
        var data = gbm.Fetch(40, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        int[] lengths = [2, 3, 5, 10, 15, 20];
        foreach (int len in lengths)
        {
            var ind = new Edcf(len);
            for (int i = 0; i < series.Count; i++)
            {
                var result = ind.Update(series[i]);
                Assert.True(double.IsFinite(result.Value),
                    $"Non-finite at bar {i} with length {len}");
            }
        }
    }
}
