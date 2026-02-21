namespace QuanTAlib.Tests;

public class HoltValidationTests
{
    private readonly GBM _gbm = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
    private readonly TSeries _series;

    public HoltValidationTests()
    {
        var bars = _gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _series = bars.Close;
    }

    /// <summary>
    /// Validates against holt.pine reference implementation logic.
    /// Manually computes Holt using the same equations.
    /// </summary>
    [Fact]
    public void Holt_MatchesPineScriptReference()
    {
        int period = 10;
        double alpha = 2.0 / (period + 1.0);
        double gamma = alpha; // gamma=0 means use alpha
        var holt = new Holt(period);

        double level = 0;
        double trend = 0;
        bool initialized = false;

        for (int i = 0; i < _series.Count; i++)
        {
            holt.Update(_series[i]);
            double src = _series[i].Value;

            if (!initialized)
            {
                level = src;
                trend = 0;
                initialized = true;
            }
            else
            {
                double prevLevel = level;
                level = (alpha * src) + ((1.0 - alpha) * (prevLevel + trend));
                trend = (gamma * (level - prevLevel)) + ((1.0 - gamma) * trend);
            }

            double expected = initialized && i > 0 ? level + trend : src;
            Assert.Equal(expected, holt.Last.Value, 9);
        }
    }

    /// <summary>
    /// Validates that constant input converges to the constant value.
    /// Level → constant, trend → 0, output → constant.
    /// </summary>
    [Fact]
    public void Holt_ConstantInput_ConvergesToValue()
    {
        double constant = 75.0;
        var holt = new Holt(20);

        for (int i = 0; i < 500; i++)
        {
            holt.Update(new TValue(DateTime.UtcNow, constant));
        }

        Assert.Equal(constant, holt.Last.Value, 6);
    }

    /// <summary>
    /// Validates deterministic output with same seed.
    /// </summary>
    [Fact]
    public void Holt_Deterministic_SameSeed()
    {
        int period = 10;

        var holt1 = new Holt(period);
        var holt2 = new Holt(period);

        for (int i = 0; i < _series.Count; i++)
        {
            holt1.Update(_series[i]);
            holt2.Update(_series[i]);
        }

        Assert.Equal(holt1.Last.Value, holt2.Last.Value, 15);
    }

    /// <summary>
    /// Validates that batch and streaming produce identical results.
    /// </summary>
    [Fact]
    public void Holt_BatchAndStreaming_Match()
    {
        int period = 15;

        var holtStream = new Holt(period);
        for (int i = 0; i < _series.Count; i++)
        {
            holtStream.Update(_series[i]);
        }

        TSeries batchResult = Holt.Batch(_series, period);

        Assert.Equal(holtStream.Last.Value, batchResult[^1].Value, 10);
    }

    /// <summary>
    /// Validates that different gamma values produce different outputs.
    /// </summary>
    [Fact]
    public void Holt_DifferentGamma_DifferentOutputs()
    {
        var bars = _gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var holt1 = new Holt(10, gamma: 0.1);
        var holt2 = new Holt(10, gamma: 0.9);

        int differenceCount = 0;
        for (int i = 0; i < series.Count; i++)
        {
            holt1.Update(series[i]);
            holt2.Update(series[i]);

            if (i > 20)
            {
                double diff = Math.Abs(holt1.Last.Value - holt2.Last.Value);
                if (diff > 1e-10)
                {
                    differenceCount++;
                }
            }
        }

        Assert.True(differenceCount > 100, $"Expected >100 different values, got {differenceCount}");
    }

    /// <summary>
    /// Validates that different periods produce different outputs.
    /// </summary>
    [Fact]
    public void Holt_DifferentPeriods_DifferentOutputs()
    {
        var bars = _gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var holt5 = new Holt(5);
        var holt50 = new Holt(50);

        int differenceCount = 0;
        for (int i = 0; i < series.Count; i++)
        {
            holt5.Update(series[i]);
            holt50.Update(series[i]);

            if (i > 50)
            {
                double diff = Math.Abs(holt5.Last.Value - holt50.Last.Value);
                if (diff > 1e-10)
                {
                    differenceCount++;
                }
            }
        }

        Assert.True(differenceCount > 100, $"Expected >100 different values, got {differenceCount}");
    }
}
