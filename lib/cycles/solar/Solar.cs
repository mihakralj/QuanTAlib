// Solar Cycle (SOLAR) - Precise solar cycle calculation using Sun's ecliptic longitude
// Calculates the seasonal position from -1.0 (winter solstice) to +1.0 (summer solstice)
// Based on astronomical algorithms for the Sun's position

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Solar Cycle indicator calculates the Sun's position in its annual cycle.
/// Output ranges from -1.0 (winter solstice) through 0.0 (equinoxes) to +1.0 (summer solstice).
/// </summary>
[SkipLocalsInit]
public sealed class Solar : AbstractBase
{
    private const double MsPerDay = 86400000.0;
    private const double JulianEpoch = 2440587.5;  // Julian date at Unix epoch (1970-01-01 00:00:00 UTC)
    private const double J2000 = 2451545.0;        // Julian date at J2000 epoch (2000-01-12 12:00:00 TT)
    private const double JulianCentury = 36525.0;  // Days per Julian century
    private const double DegToRad = Math.PI / 180.0;

    public override bool IsHot => true;  // Always hot - no warmup needed

    /// <summary>
    /// Creates a new Solar Cycle indicator.
    /// </summary>
    public Solar()
    {
        Name = "Solar";
        WarmupPeriod = 0;
        Last = new TValue(DateTime.UtcNow, 0);
    }

    /// <summary>
    /// Creates a chained Solar Cycle indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    public Solar(ITValuePublisher source) : this()
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    /// <summary>
    /// Calculates solar cycle for the given input's timestamp.
    /// </summary>
    /// <param name="input">TValue with timestamp to calculate solar cycle for</param>
    /// <param name="isNew">Not used - solar cycle is deterministic from timestamp</param>
    /// <returns>TValue with solar cycle (-1.0 to +1.0)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = CalculateCycle(input.Time);
        Last = new TValue(input.Time, value);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Calculates solar cycle for an entire TSeries.
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Times, vSpan);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Creates a new Solar indicator and calculates cycles for the source series.
    /// </summary>
    public static TSeries Calculate(TSeries source)
    {
        var solar = new Solar();
        return solar.Update(source);
    }

    /// <summary>
    /// Calculates solar cycle for a span of timestamps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<long> timestamps, Span<double> output)
    {
        if (timestamps.Length != output.Length)
        {
            throw new ArgumentException("Timestamps and output must have the same length", nameof(output));
        }

        for (int i = 0; i < timestamps.Length; i++)
        {
            output[i] = CalculateCycle(timestamps[i]);
        }
    }

    /// <summary>
    /// Calculates solar cycle for a specific DateTime.
    /// </summary>
    /// <param name="dateTime">The date/time to calculate solar cycle for</param>
    /// <returns>Solar cycle from -1.0 (winter solstice) to +1.0 (summer solstice)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateCycle(DateTime dateTime)
    {
        // Convert DateTime to Unix timestamp (milliseconds since 1970-01-01 UTC)
        long unixMs = new DateTimeOffset(dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime).ToUnixTimeMilliseconds();

        return CalculateCycle(unixMs);
    }

    /// <summary>
    /// Calculates solar cycle from Unix timestamp in milliseconds.
    /// </summary>
    /// <param name="unixMs">Unix timestamp in milliseconds</param>
    /// <returns>Solar cycle from -1.0 (winter solstice) to +1.0 (summer solstice)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateCycle(long unixMs)
    {
        // Julian Date from Unix timestamp
        double jd = Math.FusedMultiplyAdd(unixMs, 1.0 / MsPerDay, JulianEpoch);

        // Julian centuries from J2000 epoch
        double T = (jd - J2000) / JulianCentury;
        double T2 = T * T;
        double T3 = T2 * T;

        // Sun's mean longitude (L0) using FMA: 280.46646 + 36000.76983*T + 0.0003032*T²
        double L0 = NormalizeDegrees(Math.FusedMultiplyAdd(0.0003032, T2, Math.FusedMultiplyAdd(36000.76983, T, 280.46646)));

        // Sun's mean anomaly (M) using FMA: 357.52911 + 35999.05029*T - 0.0001537*T² - 0.00000025*T³
        double M = NormalizeDegrees(Math.FusedMultiplyAdd(-0.00000025, T3, Math.FusedMultiplyAdd(-0.0001537, T2, Math.FusedMultiplyAdd(35999.05029, T, 357.52911))));
        double MRad = M * DegToRad;

        // Equation of center coefficients using FMA
        double c1Coeff = Math.FusedMultiplyAdd(-0.000014, T2, Math.FusedMultiplyAdd(-0.004817, T, 1.914602));
        double c2Coeff = Math.FusedMultiplyAdd(-0.000101, T, 0.019993);

        // Equation of center (C)
        double sinM = Math.Sin(MRad);
        double sin2M = Math.Sin(2.0 * MRad);
        double sin3M = Math.Sin(3.0 * MRad);
        double C = Math.FusedMultiplyAdd(0.000289, sin3M, Math.FusedMultiplyAdd(c2Coeff, sin2M, c1Coeff * sinM));

        // Sun's true ecliptic longitude (λ)
        double lambdaSun = NormalizeDegrees(L0 + C);
        double lambdaSunRad = lambdaSun * DegToRad;

        // Solar cycle value: sin of ecliptic longitude
        // -1.0 at winter solstice (~Dec 21), 0.0 at equinoxes, +1.0 at summer solstice (~June 21)
        return Math.Sin(lambdaSunRad);
    }

    /// <summary>
    /// Normalizes angle to 0-360 degree range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NormalizeDegrees(double degrees)
    {
        double result = degrees % 360.0;
        return result < 0 ? result + 360.0 : result;
    }

    public override void Reset()
    {
        Last = new TValue(DateTime.UtcNow, 0);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        // Solar cycle doesn't use price data, so Prime is a no-op
        // Each value would just recalculate based on the current time
    }
}