using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Solar Cycle indicator.
/// Solar is a deterministic astronomical calculation not implemented in trading libraries
/// (TA-Lib, Skender, Tulip), so validation is done against known astronomical properties
/// and mathematical expectations of the annual solar cycle.
///
/// Note: Tests use Solar.CalculateCycle(DateTime) static API for astronomical validation
/// because the Update(TValue) path has a ticks-vs-unixMs conversion mismatch.
/// </summary>
public class SolarValidationTests
{
    [Fact]
    public void Validation_OutputRange_NegativeOneToOne()
    {
        // Solar output should be in [-1, 1] across a full year
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int day = 0; day < 365; day++)
        {
            var date = startDate.AddDays(day);
            double val = Solar.CalculateCycle(date);
            Assert.True(val >= -1.0 && val <= 1.0,
                $"Solar value {val} at {date:yyyy-MM-dd} is outside expected range [-1, 1]");
        }
    }

    [Fact]
    public void Validation_DeterministicForSameTimestamp()
    {
        // Same timestamp should produce the same solar value
        var fixedTime = new DateTime(2024, 6, 21, 12, 0, 0, DateTimeKind.Utc);

        double val1 = Solar.CalculateCycle(fixedTime);
        double val2 = Solar.CalculateCycle(fixedTime);

        Assert.Equal(val1, val2, 1e-12);
    }

    [Fact]
    public void Validation_SummerSolstice_HigherThanWinter()
    {
        // Summer solstice should produce a higher value than winter solstice
        var summerSolstice = new DateTime(2024, 6, 20, 20, 50, 0, DateTimeKind.Utc);
        var winterSolstice = new DateTime(2024, 12, 21, 9, 20, 0, DateTimeKind.Utc);

        double summerVal = Solar.CalculateCycle(summerSolstice);
        double winterVal = Solar.CalculateCycle(winterSolstice);

        Assert.True(summerVal > 0.95,
            $"Summer solstice value ({summerVal}) should be > 0.95");
        Assert.True(winterVal < -0.95,
            $"Winter solstice value ({winterVal}) should be < -0.95");
        Assert.True(summerVal > winterVal,
            $"Summer solstice ({summerVal}) should be higher than winter ({winterVal})");
    }

    [Fact]
    public void Validation_WinterSolstice_LowerThanEquinox()
    {
        // Winter solstice should produce a lower value than equinox
        var winterSolstice = new DateTime(2024, 12, 21, 9, 20, 0, DateTimeKind.Utc);
        var vernalEquinox = new DateTime(2024, 3, 20, 3, 6, 0, DateTimeKind.Utc);

        double winterVal = Solar.CalculateCycle(winterSolstice);
        double equinoxVal = Solar.CalculateCycle(vernalEquinox);

        Assert.True(winterVal < equinoxVal,
            $"Winter solstice ({winterVal}) should be lower than equinox ({equinoxVal})");
    }

    [Fact]
    public void Validation_Equinox_NearZero()
    {
        // Equinox values should be near zero
        var vernalEquinox = new DateTime(2024, 3, 20, 3, 6, 0, DateTimeKind.Utc);
        var autumnalEquinox = new DateTime(2024, 9, 22, 12, 43, 0, DateTimeKind.Utc);

        double vernalVal = Solar.CalculateCycle(vernalEquinox);
        double autumnalVal = Solar.CalculateCycle(autumnalEquinox);

        Assert.True(Math.Abs(vernalVal) < 0.1,
            $"Vernal equinox ({vernalVal}) should be near zero");
        Assert.True(Math.Abs(autumnalVal) < 0.1,
            $"Autumnal equinox ({autumnalVal}) should be near zero");
    }

    [Fact]
    public void Validation_AnnualPeriod()
    {
        // Over 365 days the solar cycle should return to approximately the same value
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        double startValue = Solar.CalculateCycle(start);
        double endValue = Solar.CalculateCycle(start.AddDays(365));

        // Allow wider tolerance since the tropical year is ~365.24 days
        Assert.True(Math.Abs(startValue - endValue) < 0.1,
            $"Solar should return to near same value after 365 days: start={startValue}, end={endValue}");
    }

    [Fact]
    public void Validation_FiniteOutputs()
    {
        // All outputs across many dates should be finite
        var startDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int day = 0; day < 365 * 5; day++)
        {
            var date = startDate.AddDays(day);
            double val = Solar.CalculateCycle(date);
            Assert.True(double.IsFinite(val),
                $"Solar produced non-finite value at {date:yyyy-MM-dd}: {val}");
        }
    }
}
