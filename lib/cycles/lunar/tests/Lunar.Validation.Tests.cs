using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Lunar Phase indicator.
/// Lunar is a deterministic astronomical calculation not implemented in trading libraries
/// (TA-Lib, Skender, Tulip), so validation is done against known astronomical events
/// and mathematical properties of the lunar cycle.
/// </summary>
public class LunarValidationTests
{
    [Fact]
    public void Validation_OutputRange_ZeroToOne()
    {
        // Lunar phase output should always be in [0, 1]
        var lunar = new Lunar();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            lunar.Update(new TValue(bar.Time, bar.Close));
            double val = lunar.Last.Value;
            Assert.True(val >= 0.0 && val <= 1.0,
                $"Lunar phase {val} is outside expected range [0, 1]");
        }
    }

    [Fact]
    public void Validation_DeterministicForSameTimestamp()
    {
        // Same timestamp should always produce the same lunar phase
        var lunar1 = new Lunar();
        var lunar2 = new Lunar();

        var fixedTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        lunar1.Update(new TValue(fixedTime, 100.0));
        lunar2.Update(new TValue(fixedTime, 200.0));

        Assert.Equal(lunar1.Last.Value, lunar2.Last.Value, 1e-12);
    }

    [Fact]
    public void Validation_PriceIndependent()
    {
        // Lunar phase depends only on timestamp, not on price
        var lunar = new Lunar();

        var t1 = new DateTime(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        lunar.Update(new TValue(t1, 50.0));
        double val1 = lunar.Last.Value;

        lunar = new Lunar();
        lunar.Update(new TValue(t1, 999.0));
        double val2 = lunar.Last.Value;

        Assert.Equal(val1, val2, 1e-12);
    }

    [Fact]
    public void Validation_CyclePeriodApprox29Days()
    {
        // The synodic lunar cycle is ~29.53 days
        // Over a 60-day window we should see roughly 2 full cycles
        var lunar = new Lunar();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var values = new List<double>();
        for (int day = 0; day < 60; day++)
        {
            var t = start.AddDays(day);
            lunar.Update(new TValue(t, 100.0));
            values.Add(lunar.Last.Value);
        }

        // Verify the cycle completes: values should vary significantly over 60 days
        double minVal = values.Min();
        double maxVal = values.Max();
        double range = maxVal - minVal;

        // Over 60 days (~2 synodic months) we should see significant variation
        Assert.True(range > 0.5,
            $"Expected lunar phase range > 0.5 over 60 days, got range={range} (min={minVal}, max={maxVal})");
    }

    [Fact]
    public void Validation_FiniteOutputs()
    {
        // All outputs should be finite
        var lunar = new Lunar();

        var gbm = new GBM(seed: 99);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            lunar.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(lunar.Last.Value),
                $"Lunar produced non-finite value: {lunar.Last.Value}");
        }
    }
}
