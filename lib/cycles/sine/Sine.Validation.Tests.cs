using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Ehlers Sine Wave indicator.
/// Sine is Ehlers' proprietary cycle indicator not commonly implemented in trading libraries
/// (TA-Lib, Skender, Tulip), so validation is done against mathematical properties
/// and known theoretical results based on the original algorithm.
/// </summary>
public class SineValidationTests
{
    [Fact]
    public void Validation_OutputRange_NegativeOneToOne()
    {
        // Sine wave output should be in [-1, 1]
        var sine = new Sine();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            sine.Update(new TValue(bar.Time, bar.Close));
            if (sine.IsHot)
            {
                double val = sine.Last.Value;
                Assert.True(val >= -1.0 && val <= 1.0,
                    $"Sine value {val} is outside expected range [-1, 1]");
            }
        }
    }

    [Fact]
    public void Validation_ConstantSeries_Bounded()
    {
        // For a constant price series, there is no real cycle — output should remain bounded
        var sine = new Sine();

        for (int i = 0; i < 200; i++)
        {
            sine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Constant series may not produce exactly zero due to filter initialization artifacts
        // but output should remain within the bounded range [-1, 1]
        Assert.True(sine.Last.Value >= -1.0 && sine.Last.Value <= 1.0,
            $"Constant series should produce bounded sine output, got {sine.Last.Value}");
    }

    [Fact]
    public void Validation_SinusoidInput_DetectsCycle()
    {
        // Feed a known sinusoidal signal and verify output oscillates
        var sine = new Sine(hpPeriod: 40, ssfPeriod: 10);

        var values = new List<double>();
        for (int i = 0; i < 300; i++)
        {
            double price = 100.0 + 5.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            sine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (sine.IsHot)
            {
                values.Add(sine.Last.Value);
            }
        }

        // The output should oscillate: check that it crosses zero at least once
        bool hasCrossedZero = false;
        for (int i = 1; i < values.Count; i++)
        {
            if ((values[i - 1] >= 0 && values[i] < 0) || (values[i - 1] < 0 && values[i] >= 0))
            {
                hasCrossedZero = true;
                break;
            }
        }

        Assert.True(hasCrossedZero, "Sine should oscillate (cross zero) on sinusoidal input");
    }

    [Fact]
    public void Validation_FiniteOutputs()
    {
        var sine = new Sine();

        var gbm = new GBM(seed: 99);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            sine.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(sine.Last.Value),
                $"Sine produced non-finite value: {sine.Last.Value}");
        }
    }

    [Fact]
    public void Validation_DifferentPeriods_ProduceDifferentResults()
    {
        var sine1 = new Sine(hpPeriod: 20, ssfPeriod: 5);
        var sine2 = new Sine(hpPeriod: 80, ssfPeriod: 20);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        bool foundDifference = false;
        foreach (var bar in bars)
        {
            sine1.Update(new TValue(bar.Time, bar.Close));
            sine2.Update(new TValue(bar.Time, bar.Close));
            if (sine1.IsHot && sine2.IsHot &&
                Math.Abs(sine1.Last.Value - sine2.Last.Value) > 1e-6)
            {
                foundDifference = true;
            }
        }

        Assert.True(foundDifference, "Different HP/SSF periods should produce different results");
    }
}
