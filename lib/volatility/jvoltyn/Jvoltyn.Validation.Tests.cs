// Jvoltyn: Mathematical property validation tests
// Jvoltyn is a proprietary Jurik Research indicator — no external library equivalents exist.
// Validation uses mathematical property testing: normalized output must be in [0, 100].

namespace QuanTAlib.Tests;

using Xunit;

public class JvoltynValidationTests
{
    private const int DefaultPeriod = 10;
    private const int TestDataLength = 500;

    [Fact]
    public void Jvoltyn_Output_IsFiniteForGbmData()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvoltyn = new Jvoltyn(DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            var result = jvoltyn.Update(series[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"Jvoltyn output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Jvoltyn_Output_InRange0To100_AfterWarmup()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvoltyn = new Jvoltyn(DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            var result = jvoltyn.Update(series[i], isNew: true);
            if (jvoltyn.IsHot)
            {
                Assert.True(result.Value >= -0.01 && result.Value <= 100.01,
                    $"Jvoltyn output must be in [0, 100] after warmup at bar {i}, got {result.Value}");
            }
        }
    }

    [Fact]
    public void Jvoltyn_ConstantSeries_ZeroNormalizedVolatility()
    {
        var jvoltyn = new Jvoltyn(DefaultPeriod);
        double price = 100.0;

        // Feed constant-price values
        for (int i = 0; i < 300; i++)
        {
            jvoltyn.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price), isNew: true);
        }

        // Constant series: d = 1 → normalized = (1-1)/(logParam-1)*100 = 0
        Assert.Equal(0.0, jvoltyn.Last.Value, precision: 1);
    }

    [Fact]
    public void Jvoltyn_FirstBar_ReturnsZero()
    {
        var jvoltyn = new Jvoltyn(DefaultPeriod);

        var result = jvoltyn.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);

        // First bar initializes bands to price, d=1 → normalized=0
        Assert.Equal(0.0, result.Value, precision: 10);
    }

    [Fact]
    public void Jvoltyn_UpperBand_GreaterOrEqualLowerBand()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvoltyn = new Jvoltyn(DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            jvoltyn.Update(series[i], isNew: true);

            Assert.True(jvoltyn.UpperBand >= jvoltyn.LowerBand,
                $"UpperBand ({jvoltyn.UpperBand}) must be >= LowerBand ({jvoltyn.LowerBand}) at bar {i}");
        }
    }

    [Fact]
    public void Jvoltyn_RawVolatility_IsConsistentWithNormalized()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvoltyn = new Jvoltyn(DefaultPeriod);

        // Calculate logParam manually to verify normalization
        double lengthParam = (DefaultPeriod - 1.0) / 2.0;
        double logParam = System.Math.Log(System.Math.Sqrt(lengthParam)) / System.Math.Log(2.0);
        logParam = (logParam + 2.0) < 0.0 ? 0.0 : (logParam + 2.0);
        double normFactor = System.Math.Abs(logParam - 1.0) > 1e-10 ? 100.0 / (logParam - 1.0) : 0.0;

        for (int i = 0; i < series.Count; i++)
        {
            jvoltyn.Update(series[i], isNew: true);

            if (i > 0) // Skip first bar initialization
            {
                double expectedNormalized = (jvoltyn.RawVolatility - 1.0) * normFactor;
                Assert.Equal(expectedNormalized, jvoltyn.Last.Value, precision: 8);
            }
        }
    }

    [Fact]
    public void Jvoltyn_BatchAndStreaming_ProduceSameResults()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Batch
        var batchResults = Jvoltyn.Batch(series, DefaultPeriod);

        // Streaming
        var streamJvoltyn = new Jvoltyn(DefaultPeriod);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            var result = streamJvoltyn.Update(series[i], isNew: true);
            streamResults[i] = result.Value;
        }

        Assert.Equal(batchResults.Count, series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 10);
        }
    }

    [Fact]
    public void Jvoltyn_SpanAndStreaming_ProduceSameResults()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var spanOutput = new double[series.Count];

        Jvoltyn.Batch(series.Values, spanOutput, DefaultPeriod);

        // Streaming
        var streamJvoltyn = new Jvoltyn(DefaultPeriod);
        for (int i = 0; i < series.Count; i++)
        {
            streamJvoltyn.Update(series[i], isNew: true);
            Assert.Equal(spanOutput[i], streamJvoltyn.Last.Value, precision: 10);
        }
    }

    [Fact]
    public void Jvoltyn_DifferentPeriods_ProduceDifferentResults()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        var jvoltyn5 = new Jvoltyn(5);
        var jvoltyn50 = new Jvoltyn(50);

        for (int i = 0; i < series.Count; i++)
        {
            jvoltyn5.Update(series[i], isNew: true);
            jvoltyn50.Update(series[i], isNew: true);
        }

        Assert.NotEqual(jvoltyn5.Last.Value, jvoltyn50.Last.Value);
    }

    [Fact]
    public void Jvoltyn_BarCorrection_IsNewFalse_RestoresState()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvoltyn = new Jvoltyn(DefaultPeriod);

        for (int i = 0; i < 30; i++)
        {
            jvoltyn.Update(series[i], isNew: true);
        }

        jvoltyn.Update(series[30], isNew: true);
        double afterNew = jvoltyn.Last.Value;

        jvoltyn.Update(series[30], isNew: false);
        double afterCorrection = jvoltyn.Last.Value;

        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }
}
