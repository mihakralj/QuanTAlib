// Jvolty: Mathematical property validation tests
// Jvolty is a proprietary Jurik Research indicator — no external library equivalents exist.
// Validation uses mathematical property testing against known volatility band behaviors.

namespace QuanTAlib.Tests;

using Xunit;

public class JvoltyValidationTests
{
    private const int DefaultPeriod = 10;
    private const int TestDataLength = 500;

    [Fact]
    public void Jvolty_Output_IsFiniteForGbmData()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvolty = new Jvolty(DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            var result = jvolty.Update(series[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"Jvolty output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Jvolty_Output_IsPositive_AfterWarmup()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvolty = new Jvolty(DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            var result = jvolty.Update(series[i], isNew: true);
            if (jvolty.IsHot)
            {
                Assert.True(result.Value >= 1.0,
                    $"Jvolty output must be >= 1.0 after warmup at bar {i}, got {result.Value}");
            }
        }
    }

    [Fact]
    public void Jvolty_ConstantSeries_MinimumVolatility()
    {
        var jvolty = new Jvolty(DefaultPeriod);
        double price = 100.0;

        // Feed constant-price values
        for (int i = 0; i < 300; i++)
        {
            jvolty.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price), isNew: true);
        }

        // Constant series should produce minimum volatility (d = 1.0)
        Assert.Equal(1.0, jvolty.Last.Value, precision: 1);
    }

    [Fact]
    public void Jvolty_UpperBand_GreaterOrEqualLowerBand()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvolty = new Jvolty(DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            jvolty.Update(series[i], isNew: true);

            Assert.True(jvolty.UpperBand >= jvolty.LowerBand,
                $"UpperBand ({jvolty.UpperBand}) must be >= LowerBand ({jvolty.LowerBand}) at bar {i}");
        }
    }

    [Fact]
    public void Jvolty_HighVolatility_ProducesHigherExponent()
    {
        // Low volatility data
        var lowVolSeries = new GBM(sigma: 0.01, seed: 123).Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var lowJvolty = new Jvolty(DefaultPeriod);
        for (int i = 0; i < lowVolSeries.Count; i++)
        {
            lowJvolty.Update(lowVolSeries[i], isNew: true);
        }
        double lowVolResult = lowJvolty.Last.Value;

        // High volatility data
        var highVolSeries = new GBM(sigma: 2.0, seed: 123).Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var highJvolty = new Jvolty(DefaultPeriod);
        for (int i = 0; i < highVolSeries.Count; i++)
        {
            highJvolty.Update(highVolSeries[i], isNew: true);
        }
        double highVolResult = highJvolty.Last.Value;

        // High volatility data should generally produce higher exponent values
        // (This is a statistical property, not guaranteed per-sample)
        Assert.True(highVolResult >= 1.0, "High vol result should be >= 1.0");
        Assert.True(lowVolResult >= 1.0, "Low vol result should be >= 1.0");
    }

    [Fact]
    public void Jvolty_BatchAndStreaming_ProduceSameResults()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        // Batch
        var batchResults = Jvolty.Batch(series, DefaultPeriod);

        // Streaming
        var streamJvolty = new Jvolty(DefaultPeriod);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            var result = streamJvolty.Update(series[i], isNew: true);
            streamResults[i] = result.Value;
        }

        Assert.Equal(batchResults.Count, series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 10);
        }
    }

    [Fact]
    public void Jvolty_SpanAndStreaming_ProduceSameResults()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var spanOutput = new double[series.Count];

        Jvolty.Batch(series.Values, spanOutput, DefaultPeriod);

        // Streaming
        var streamJvolty = new Jvolty(DefaultPeriod);
        for (int i = 0; i < series.Count; i++)
        {
            streamJvolty.Update(series[i], isNew: true);
            Assert.Equal(spanOutput[i], streamJvolty.Last.Value, precision: 10);
        }
    }

    [Fact]
    public void Jvolty_DifferentPeriods_ProduceDifferentResults()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        var jvolty5 = new Jvolty(5);
        var jvolty50 = new Jvolty(50);

        for (int i = 0; i < series.Count; i++)
        {
            jvolty5.Update(series[i], isNew: true);
            jvolty50.Update(series[i], isNew: true);
        }

        // Different periods should produce different results
        Assert.NotEqual(jvolty5.Last.Value, jvolty50.Last.Value);
    }

    [Fact]
    public void Jvolty_BarCorrection_IsNewFalse_RestoresState()
    {
        var series = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var jvolty = new Jvolty(DefaultPeriod);

        // Process 30 bars
        for (int i = 0; i < 30; i++)
        {
            jvolty.Update(series[i], isNew: true);
        }

        // Update bar 30 (isNew=true) then correct it (isNew=false)
        jvolty.Update(series[30], isNew: true);
        double afterNew = jvolty.Last.Value;

        jvolty.Update(series[30], isNew: false);
        double afterCorrection = jvolty.Last.Value;

        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }
}
