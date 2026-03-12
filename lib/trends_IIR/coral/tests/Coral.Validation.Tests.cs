namespace QuanTAlib.Tests;

public class CoralValidationTests
{
    private readonly GBM _gbm;

    public CoralValidationTests()
    {
        _gbm = new GBM();
    }

    /// <summary>
    /// Validates that Coral output matches a manual PineScript-equivalent reference calculation.
    /// The reference manually cascades 6 EMAs and applies the polynomial combination.
    /// </summary>
    [Fact]
    public void Coral_MatchesPineScriptReference()
    {
        const int period = 21;
        const double cd = 0.4;
        var bars = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // QuanTAlib Coral
        var coral = new Coral(period, cd);
        for (int i = 0; i < series.Count; i++)
        {
            coral.Update(series[i]);
        }

        // Manual reference calculation matching coral.pine
        double di = ((period - 1.0) / 2.0) + 1.0;
        double c1 = 2.0 / (di + 1.0);
        double c2 = 1.0 - c1;
        double cd2 = cd * cd;
        double cd3 = cd2 * cd;
        double c3 = 3.0 * (cd2 + cd3);
        double c4 = -3.0 * ((2.0 * cd2) + cd + cd3);
        double c5 = (3.0 * cd) + 1.0 + cd3 + (3.0 * cd2);

        double i1 = 0, i2 = 0, i3 = 0, i4 = 0, i5 = 0, i6 = 0;
        double bfr = 0;

        var values = series.Values.ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            double src = values[i];
            i1 = (c1 * src) + (c2 * i1);
            i2 = (c1 * i1) + (c2 * i2);
            i3 = (c1 * i2) + (c2 * i3);
            i4 = (c1 * i3) + (c2 * i4);
            i5 = (c1 * i4) + (c2 * i5);
            i6 = (c1 * i5) + (c2 * i6);
            bfr = (-cd3 * i6) + (c3 * i5) + (c4 * i4) + (c5 * i3);
        }

        Assert.Equal(bfr, coral.Last.Value, 1e-9);
    }

    /// <summary>
    /// Validates unity DC gain: for constant input, c3 + c4 + c5 + (-cd³) = 1
    /// means the output converges exactly to the input.
    /// </summary>
    [Fact]
    public void Coral_UnityDcGain_ConvergesToConstant()
    {
        const double constant = 42.0;
        double[] cdValues = [0.0, 0.2, 0.4, 0.6, 0.8, 1.0];

        foreach (double cd in cdValues)
        {
            var coral = new Coral(10, cd);
            for (int i = 0; i < 500; i++)
            {
                coral.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constant));
            }
            Assert.Equal(constant, coral.Last.Value, 4);
        }
    }

    /// <summary>
    /// Validates that Batch(TSeries), Batch(Span), streaming, and eventing all agree.
    /// </summary>
    [Fact]
    public void Coral_AllModes_Consistent()
    {
        const int period = 14;
        const double cd = 0.5;
        var bars = _gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Batch TSeries
        var batchResult = Coral.Batch(series, period, cd);

        // Batch Span
        var srcArray = series.Values.ToArray();
        var spanOutput = new double[srcArray.Length];
        Coral.Batch(srcArray.AsSpan(), spanOutput.AsSpan(), period, cd);

        // Streaming
        var streaming = new Coral(period, cd);
        for (int i = 0; i < series.Count; i++)
        {
            streaming.Update(series[i]);
        }

        // Eventing
        var pubSource = new TSeries();
        var eventing = new Coral(pubSource, period, cd);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }

        // Compare last values
        Assert.Equal(batchResult.Last.Value, spanOutput[^1], 1e-9);
        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 1e-9);
        Assert.Equal(batchResult.Last.Value, eventing.Last.Value, 1e-9);

        // Compare all values (batch vs span)
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-9);
        }
    }

    /// <summary>
    /// Validates determinism: same inputs always produce same outputs.
    /// </summary>
    [Fact]
    public void Coral_Deterministic()
    {
        const int period = 10;
        var bars = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var coral1 = new Coral(period);
        var coral2 = new Coral(period);

        for (int i = 0; i < series.Count; i++)
        {
            coral1.Update(series[i]);
            coral2.Update(series[i]);
        }

        Assert.Equal(coral1.Last.Value, coral2.Last.Value, 15);
    }

    /// <summary>
    /// Validates that different periods produce different outputs.
    /// </summary>
    [Fact]
    public void Coral_DifferentPeriods_ProduceDifferentOutputs()
    {
        var bars = _gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var coral5 = new Coral(5);
        var coral50 = new Coral(50);

        int differenceCount = 0;
        for (int i = 0; i < series.Count; i++)
        {
            coral5.Update(series[i]);
            coral50.Update(series[i]);

            if (i > 50) // After warmup
            {
                double diff = Math.Abs(coral5.Last.Value - coral50.Last.Value);
                if (diff > 1e-10)
                {
                    differenceCount++;
                }
            }
        }

        // Different periods must produce meaningfully different outputs
        Assert.True(differenceCount > 100, $"Expected >100 different values, got {differenceCount}");
    }
}
