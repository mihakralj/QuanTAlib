namespace QuanTAlib.Tests;

using Xunit;

public class SolarTests
{
    private const double Tolerance = 1e-6;

    // Known solar dates:
    // Winter Solstice (~Dec 21): value ≈ -1.0
    // Vernal Equinox (~Mar 20): value ≈ 0.0 (rising)
    // Summer Solstice (~Jun 21): value ≈ +1.0
    // Autumnal Equinox (~Sep 22): value ≈ 0.0 (falling)

    [Fact]
    public void Solar_ConstructorDefaults()
    {
        var solar = new Solar();
        Assert.Equal("Solar", solar.Name);
        Assert.Equal(0, solar.WarmupPeriod);
        Assert.True(solar.IsHot);
    }

    [Fact]
    public void Solar_Update_ReturnsValidCycle()
    {
        var solar = new Solar();
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = solar.Update(input);

        Assert.True(result.Value >= -1.0 && result.Value <= 1.0);
        Assert.Equal(input.Time, result.Time);
    }

    [Fact]
    public void Solar_WinterSolstice_ReturnsNegativeValue()
    {
        // December 21, 2024 - Winter Solstice at 09:20 UTC
        var winterSolstice = new DateTime(2024, 12, 21, 9, 20, 0, DateTimeKind.Utc);
        double cycle = Solar.CalculateCycle(winterSolstice);

        // Winter solstice should be close to -1.0
        Assert.True(cycle < -0.95, $"Expected cycle < -0.95 at winter solstice, got {cycle}");
    }

    [Fact]
    public void Solar_SummerSolstice_ReturnsPositiveValue()
    {
        // June 20, 2024 - Summer Solstice at 20:50 UTC
        var summerSolstice = new DateTime(2024, 6, 20, 20, 50, 0, DateTimeKind.Utc);
        double cycle = Solar.CalculateCycle(summerSolstice);

        // Summer solstice should be close to +1.0
        Assert.True(cycle > 0.95, $"Expected cycle > 0.95 at summer solstice, got {cycle}");
    }

    [Fact]
    public void Solar_VernalEquinox_ReturnsNearZero()
    {
        // March 20, 2024 - Vernal Equinox at 03:06 UTC
        var vernalEquinox = new DateTime(2024, 3, 20, 3, 6, 0, DateTimeKind.Utc);
        double cycle = Solar.CalculateCycle(vernalEquinox);

        // Vernal equinox should be near 0 (slightly positive, rising)
        Assert.True(Math.Abs(cycle) < 0.1, $"Expected cycle ~0 at vernal equinox, got {cycle}");
    }

    [Fact]
    public void Solar_AutumnalEquinox_ReturnsNearZero()
    {
        // September 22, 2024 - Autumnal Equinox at 12:43 UTC
        var autumnalEquinox = new DateTime(2024, 9, 22, 12, 43, 0, DateTimeKind.Utc);
        double cycle = Solar.CalculateCycle(autumnalEquinox);

        // Autumnal equinox should be near 0 (slightly negative, falling)
        Assert.True(Math.Abs(cycle) < 0.1, $"Expected cycle ~0 at autumnal equinox, got {cycle}");
    }

    [Fact]
    public void Solar_YearCycle_CoversFullRange()
    {
        // Sample through a full year
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double minValue = double.MaxValue;
        double maxValue = double.MinValue;

        for (int day = 0; day < 365; day++)
        {
            var date = startDate.AddDays(day);
            double cycle = Solar.CalculateCycle(date);
            minValue = Math.Min(minValue, cycle);
            maxValue = Math.Max(maxValue, cycle);
        }

        // Should cover nearly the full range
        Assert.True(minValue < -0.95, $"Min value should be < -0.95, got {minValue}");
        Assert.True(maxValue > 0.95, $"Max value should be > 0.95, got {maxValue}");
    }

    [Fact]
    public void Solar_Batch_MatchesStreaming()
    {
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        int count = 100;

        // Create timestamps
        var timestamps = new long[count];
        var expected = new double[count];

        for (int i = 0; i < count; i++)
        {
            var date = startDate.AddDays(i);
            timestamps[i] = new DateTimeOffset(date).ToUnixTimeMilliseconds();
            expected[i] = Solar.CalculateCycle(date);
        }

        // Calculate using batch
        var output = new double[count];
        Solar.Batch(timestamps, output);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], output[i], Tolerance);
        }
    }

    [Fact]
    public void Solar_TSeries_Update()
    {
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var series = new TSeries(30);

        for (int i = 0; i < 30; i++)
        {
            series.Add(new TValue(startDate.AddDays(i), 100.0 + i));
        }

        var solar = new Solar();
        var result = solar.Update(series);

        Assert.Equal(30, result.Count);

        // Verify each value
        for (int i = 0; i < 30; i++)
        {
            double expectedCycle = Solar.CalculateCycle(series[i].Time);
            Assert.Equal(expectedCycle, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Solar_StaticCalculate_TSeries()
    {
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var series = new TSeries(30);

        for (int i = 0; i < 30; i++)
        {
            series.Add(new TValue(startDate.AddDays(i), 100.0 + i));
        }

        var result = Solar.Calculate(series);

        Assert.Equal(30, result.Count);

        for (int i = 0; i < 30; i++)
        {
            double expectedCycle = Solar.CalculateCycle(series[i].Time);
            Assert.Equal(expectedCycle, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Solar_Chaining_Works()
    {
        var source = new Sma(10);
        var solar = new Solar(source);

        bool eventFired = false;
        solar.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var input = new TValue(DateTime.UtcNow, 100.0);
        source.Update(input);

        Assert.True(eventFired);
    }

    [Fact]
    public void Solar_Reset()
    {
        var solar = new Solar();
        var input = new TValue(DateTime.UtcNow, 100.0);
        solar.Update(input);

        solar.Reset();

        // After reset, Last should be reset
        Assert.Equal(0, solar.Last.Value);
    }

    [Fact]
    public void Solar_UnixTimestamp_CalculatesCorrectly()
    {
        // Test using known Unix timestamp
        // January 1, 2024 00:00:00 UTC = 1704067200000 ms
        long unixMs = 1704067200000;
        double cycle1 = Solar.CalculateCycle(unixMs);

        var dateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double cycle2 = Solar.CalculateCycle(dateTime);

        Assert.Equal(cycle1, cycle2, Tolerance);
    }

    [Fact]
    public void Solar_Cycle_AlwaysInRange()
    {
        // Test across multiple years
        var startDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int day = 0; day < 365 * 5; day++) // 5 years
        {
            var date = startDate.AddDays(day);
            double cycle = Solar.CalculateCycle(date);

            Assert.True(cycle >= -1.0 && cycle <= 1.0,
                $"Cycle out of range at {date}: {cycle}");
        }
    }

    [Fact]
    public void Solar_Batch_ThrowsOnLengthMismatch()
    {
        var timestamps = new long[10];
        var output = new double[5];

        Assert.Throws<ArgumentException>(() => Solar.Batch(timestamps, output));
    }

    [Fact]
    public void Solar_EmptyTSeries_ReturnsEmpty()
    {
        var solar = new Solar();
        var empty = new TSeries();
        var result = solar.Update(empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Solar_IsNew_Parameter_DoesNotAffectResult()
    {
        var solar = new Solar();
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result1 = solar.Update(input, isNew: true);

        solar.Reset();

        var result2 = solar.Update(input, isNew: false);

        // Solar cycle is deterministic from timestamp, isNew shouldn't matter
        Assert.Equal(result1.Value, result2.Value, Tolerance);
    }

    [Fact]
    public void Solar_DateTimeKind_Unspecified_TreatedAsUtc()
    {
        var unspecified = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        double cycle1 = Solar.CalculateCycle(unspecified);
        double cycle2 = Solar.CalculateCycle(utc);

        Assert.Equal(cycle1, cycle2, Tolerance);
    }

    [Fact]
    public void Solar_Historical_WinterSolstice_2000()
    {
        // December 21, 2000 - Winter Solstice at 13:37 UTC
        var winterSolstice = new DateTime(2000, 12, 21, 13, 37, 0, DateTimeKind.Utc);
        double cycle = Solar.CalculateCycle(winterSolstice);

        Assert.True(cycle < -0.95, $"Expected cycle < -0.95 at 2000 winter solstice, got {cycle}");
    }

    [Fact]
    public void Solar_Historical_SummerSolstice_2000()
    {
        // June 21, 2000 - Summer Solstice at 01:48 UTC
        var summerSolstice = new DateTime(2000, 6, 21, 1, 48, 0, DateTimeKind.Utc);
        double cycle = Solar.CalculateCycle(summerSolstice);

        Assert.True(cycle > 0.95, $"Expected cycle > 0.95 at 2000 summer solstice, got {cycle}");
    }
}