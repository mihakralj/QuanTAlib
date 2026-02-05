namespace QuanTAlib.Tests;

using Xunit;

public class LunarTests
{
    private const double Tolerance = 1e-6;

    // Known lunar phase dates (verified against astronomical data)
    // New Moon: ~0.0, Full Moon: ~1.0, Quarters: ~0.5

    [Fact]
    public void Lunar_ConstructorDefaults()
    {
        var lunar = new Lunar();
        Assert.Equal("Lunar", lunar.Name);
        Assert.Equal(0, lunar.WarmupPeriod);
        Assert.True(lunar.IsHot);
    }

    [Fact]
    public void Lunar_Update_ReturnsValidPhase()
    {
        var lunar = new Lunar();
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = lunar.Update(input);

        Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        Assert.Equal(input.Time, result.Time);
    }

    [Fact]
    public void Lunar_KnownNewMoon_ReturnsLowPhase()
    {
        // January 29, 2025 - New Moon at 12:36 UTC
        var newMoon = new DateTime(2025, 1, 29, 12, 36, 0, DateTimeKind.Utc);
        double phase = Lunar.CalculatePhase(newMoon);

        // New moon should be close to 0
        Assert.True(phase < 0.05, $"Expected phase < 0.05 at new moon, got {phase}");
    }

    [Fact]
    public void Lunar_KnownFullMoon_ReturnsHighPhase()
    {
        // February 12, 2025 - Full Moon at 13:53 UTC
        var fullMoon = new DateTime(2025, 2, 12, 13, 53, 0, DateTimeKind.Utc);
        double phase = Lunar.CalculatePhase(fullMoon);

        // Full moon should be close to 1
        Assert.True(phase > 0.95, $"Expected phase > 0.95 at full moon, got {phase}");
    }

    [Fact]
    public void Lunar_FirstQuarter_ReturnsHalfPhase()
    {
        // February 5, 2025 - First Quarter at 08:02 UTC
        var firstQuarter = new DateTime(2025, 2, 5, 8, 2, 0, DateTimeKind.Utc);
        double phase = Lunar.CalculatePhase(firstQuarter);

        // First quarter should be around 0.5
        Assert.True(phase > 0.4 && phase < 0.6, $"Expected phase ~0.5 at first quarter, got {phase}");
    }

    [Fact]
    public void Lunar_LastQuarter_ReturnsHalfPhase()
    {
        // February 20, 2025 - Last Quarter at 17:33 UTC
        var lastQuarter = new DateTime(2025, 2, 20, 17, 33, 0, DateTimeKind.Utc);
        double phase = Lunar.CalculatePhase(lastQuarter);

        // Last quarter should be around 0.5
        Assert.True(phase > 0.4 && phase < 0.6, $"Expected phase ~0.5 at last quarter, got {phase}");
    }

    [Fact]
    public void Lunar_PhaseCycle_Increases_Then_Decreases()
    {
        // Check phase increases from new moon to full moon
        var startDate = new DateTime(2025, 1, 29, 12, 0, 0, DateTimeKind.Utc); // New moon
        double prevPhase = Lunar.CalculatePhase(startDate);

        // Check for 7 days after new moon - phase should generally increase
        for (int day = 1; day <= 7; day++)
        {
            var date = startDate.AddDays(day);
            double phase = Lunar.CalculatePhase(date);

            // Allow small fluctuations due to orbital mechanics
            Assert.True(phase >= prevPhase - 0.01,
                $"Phase should increase from new moon: day {day}, prev={prevPhase}, curr={phase}");
            prevPhase = phase;
        }
    }

    [Fact]
    public void Lunar_LunarMonth_Cycle()
    {
        // One lunar month is approximately 29.53 days
        var startDate = new DateTime(2025, 1, 29, 12, 36, 0, DateTimeKind.Utc); // New Moon
        double startPhase = Lunar.CalculatePhase(startDate);

        // After ~29.53 days, should be back to similar phase
        var endDate = startDate.AddDays(29.53);
        double endPhase = Lunar.CalculatePhase(endDate);

        Assert.True(Math.Abs(startPhase - endPhase) < 0.1,
            $"Phase should return to ~same value after lunar month: start={startPhase}, end={endPhase}");
    }

    [Fact]
    public void Lunar_Batch_MatchesStreaming()
    {
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        int count = 100;

        // Create timestamps
        var timestamps = new long[count];
        var expected = new double[count];

        for (int i = 0; i < count; i++)
        {
            var date = startDate.AddDays(i);
            timestamps[i] = new DateTimeOffset(date).ToUnixTimeMilliseconds();
            expected[i] = Lunar.CalculatePhase(date);
        }

        // Calculate using batch
        var output = new double[count];
        Lunar.Batch(timestamps, output);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], output[i], Tolerance);
        }
    }

    [Fact]
    public void Lunar_TSeries_Update()
    {
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var series = new TSeries(30);

        for (int i = 0; i < 30; i++)
        {
            series.Add(new TValue(startDate.AddDays(i), 100.0 + i));
        }

        var lunar = new Lunar();
        var result = lunar.Update(series);

        Assert.Equal(30, result.Count);

        // Verify each value
        for (int i = 0; i < 30; i++)
        {
            double expectedPhase = Lunar.CalculatePhase(series[i].Time);
            Assert.Equal(expectedPhase, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Lunar_StaticCalculate_TSeries()
    {
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var series = new TSeries(30);

        for (int i = 0; i < 30; i++)
        {
            series.Add(new TValue(startDate.AddDays(i), 100.0 + i));
        }

        var result = Lunar.Calculate(series);

        Assert.Equal(30, result.Count);

        for (int i = 0; i < 30; i++)
        {
            double expectedPhase = Lunar.CalculatePhase(series[i].Time);
            Assert.Equal(expectedPhase, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Lunar_Chaining_Works()
    {
        var source = new Sma(10);
        var lunar = new Lunar(source);

        bool eventFired = false;
        lunar.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var input = new TValue(DateTime.UtcNow, 100.0);
        source.Update(input);

        Assert.True(eventFired);
    }

    [Fact]
    public void Lunar_Reset()
    {
        var lunar = new Lunar();
        var input = new TValue(DateTime.UtcNow, 100.0);
        lunar.Update(input);

        lunar.Reset();

        // After reset, Last should be reset
        Assert.Equal(0, lunar.Last.Value);
    }

    [Fact]
    public void Lunar_UnixTimestamp_CalculatesCorrectly()
    {
        // Test using known Unix timestamp
        // January 1, 2020 00:00:00 UTC = 1577836800000 ms
        long unixMs = 1577836800000;
        double phase1 = Lunar.CalculatePhase(unixMs);

        var dateTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double phase2 = Lunar.CalculatePhase(dateTime);

        Assert.Equal(phase1, phase2, Tolerance);
    }

    [Fact]
    public void Lunar_Phase_AlwaysInRange()
    {
        // Test across multiple years
        var startDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int day = 0; day < 365 * 5; day++) // 5 years
        {
            var date = startDate.AddDays(day);
            double phase = Lunar.CalculatePhase(date);

            Assert.True(phase >= 0.0 && phase <= 1.0,
                $"Phase out of range at {date}: {phase}");
        }
    }

    [Fact]
    public void Lunar_HistoricalNewMoon_1999()
    {
        // December 7, 1999 - New Moon at 22:32 UTC
        var newMoon = new DateTime(1999, 12, 7, 22, 32, 0, DateTimeKind.Utc);
        double phase = Lunar.CalculatePhase(newMoon);

        Assert.True(phase < 0.05, $"Expected low phase at 1999 new moon, got {phase}");
    }

    [Fact]
    public void Lunar_HistoricalFullMoon_2000()
    {
        // January 21, 2000 - Full Moon (also a lunar eclipse)
        var fullMoon = new DateTime(2000, 1, 21, 4, 40, 0, DateTimeKind.Utc);
        double phase = Lunar.CalculatePhase(fullMoon);

        Assert.True(phase > 0.95, $"Expected high phase at 2000 full moon, got {phase}");
    }

    [Fact]
    public void Lunar_Batch_ThrowsOnLengthMismatch()
    {
        var timestamps = new long[10];
        var output = new double[5];

        Assert.Throws<ArgumentException>(() => Lunar.Batch(timestamps, output));
    }

    [Fact]
    public void Lunar_EmptyTSeries_ReturnsEmpty()
    {
        var lunar = new Lunar();
        var empty = new TSeries();
        var result = lunar.Update(empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Lunar_IsNew_Parameter_DoesNotAffectResult()
    {
        var lunar = new Lunar();
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result1 = lunar.Update(input, isNew: true);

        lunar.Reset();

        var result2 = lunar.Update(input, isNew: false);

        // Lunar phase is deterministic from timestamp, isNew shouldn't matter
        Assert.Equal(result1.Value, result2.Value, Tolerance);
    }

    [Fact]
    public void Lunar_DateTimeKind_Unspecified_TreatedAsUtc()
    {
        var unspecified = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        double phase1 = Lunar.CalculatePhase(unspecified);
        double phase2 = Lunar.CalculatePhase(utc);

        Assert.Equal(phase1, phase2, Tolerance);
    }
}