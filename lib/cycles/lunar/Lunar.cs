// Lunar Phase (LUNAR) - Precise lunar phase calculation using orbital mechanics
// Calculates the Moon's illumination phase from 0.0 (new moon) to 1.0 (full moon)
// Based on Meeus astronomical algorithms with perturbation corrections

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Lunar Phase indicator calculates the Moon's illumination phase using orbital mechanics.
/// Output ranges from 0.0 (new moon) through 0.5 (first/last quarter) to 1.0 (full moon).
/// </summary>
[SkipLocalsInit]
public sealed class Lunar : AbstractBase
{
    private const double MsPerDay = 86400000.0;
    private const double JulianEpoch = 2440587.5;  // Julian date at Unix epoch (1970-01-01 00:00:00 UTC)
    private const double J2000 = 2451545.0;        // Julian date at J2000 epoch (2000-01-12 12:00:00 TT)
    private const double JulianCentury = 36525.0;  // Days per Julian century
    private const double DegToRad = Math.PI / 180.0;

    public override bool IsHot => true;  // Always hot - no warmup needed

    /// <summary>
    /// Creates a new Lunar Phase indicator.
    /// </summary>
    public Lunar()
    {
        Name = "Lunar";
        WarmupPeriod = 0;
        Last = new TValue(DateTime.UtcNow, 0);
    }

    /// <summary>
    /// Creates a chained Lunar Phase indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    public Lunar(ITValuePublisher source) : this()
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
    /// Calculates lunar phase for the given input's timestamp.
    /// </summary>
    /// <param name="input">TValue with timestamp to calculate lunar phase for</param>
    /// <param name="isNew">Not used - lunar phase is deterministic from timestamp</param>
    /// <returns>TValue with lunar phase (0.0 to 1.0)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double phase = CalculatePhase(input.Time);
        Last = new TValue(input.Time, phase);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Calculates lunar phase for an entire TSeries.
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
    /// Creates a new Lunar indicator and calculates phases for the source series.
    /// </summary>
    public static TSeries Calculate(TSeries source)
    {
        var lunar = new Lunar();
        return lunar.Update(source);
    }

    /// <summary>
    /// Calculates lunar phase for a span of timestamps.
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
            output[i] = CalculatePhase(timestamps[i]);
        }
    }

    /// <summary>
    /// Calculates lunar phase for a specific DateTime.
    /// </summary>
    /// <param name="dateTime">The date/time to calculate lunar phase for</param>
    /// <returns>Lunar phase from 0.0 (new moon) to 1.0 (full moon)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculatePhase(DateTime dateTime)
    {
        // Convert DateTime to Unix timestamp (milliseconds since 1970-01-01 UTC)
        long unixMs = new DateTimeOffset(dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime).ToUnixTimeMilliseconds();

        return CalculatePhase(unixMs);
    }

    /// <summary>
    /// Calculates lunar phase from Unix timestamp in milliseconds.
    /// </summary>
    /// <param name="unixMs">Unix timestamp in milliseconds</param>
    /// <returns>Lunar phase from 0.0 (new moon) to 1.0 (full moon)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculatePhase(long unixMs)
    {
        // Julian Date from Unix timestamp using FMA
        double jd = Math.FusedMultiplyAdd(unixMs, 1.0 / MsPerDay, JulianEpoch);

        // Julian centuries from J2000 epoch
        double T = (jd - J2000) / JulianCentury;
        double T2 = T * T;
        double T3 = T2 * T;
        double T4 = T3 * T;

        // Moon's mean longitude (Lp) using FMA chain
        // 218.3164477 + 481267.88123421*T - 0.0015786*T² + T³/538841 - T⁴/65194000
        double Lp = NormalizeDegrees(
            Math.FusedMultiplyAdd(-1.0 / 65194000.0, T4,
            Math.FusedMultiplyAdd(1.0 / 538841.0, T3,
            Math.FusedMultiplyAdd(-0.0015786, T2,
            Math.FusedMultiplyAdd(481267.88123421, T, 218.3164477)))));

        // Mean elongation of the Moon (D) using FMA chain
        double D = NormalizeDegrees(
            Math.FusedMultiplyAdd(-1.0 / 113065000.0, T4,
            Math.FusedMultiplyAdd(1.0 / 545868.0, T3,
            Math.FusedMultiplyAdd(-0.0018819, T2,
            Math.FusedMultiplyAdd(445267.1114034, T, 297.8501921)))));

        // Sun's mean anomaly (M) using FMA chain
        double M = NormalizeDegrees(
            Math.FusedMultiplyAdd(1.0 / 24490000.0, T3,
            Math.FusedMultiplyAdd(-0.0001536, T2,
            Math.FusedMultiplyAdd(35999.0502909, T, 357.5291092))));

        // Moon's mean anomaly (Mp) using FMA chain
        double Mp = NormalizeDegrees(
            Math.FusedMultiplyAdd(-1.0 / 14712000.0, T4,
            Math.FusedMultiplyAdd(1.0 / 69699.0, T3,
            Math.FusedMultiplyAdd(0.0087414, T2,
            Math.FusedMultiplyAdd(477198.8675055, T, 134.9633964)))));

        // Moon's argument of latitude (F) using FMA chain
        double F = NormalizeDegrees(
            Math.FusedMultiplyAdd(1.0 / 863310000.0, T4,
            Math.FusedMultiplyAdd(-1.0 / 3526000.0, T3,
            Math.FusedMultiplyAdd(-0.0036539, T2,
            Math.FusedMultiplyAdd(483202.0175233, T, 93.2720950)))));

        // Convert to radians for trigonometric functions
        double DRad = D * DegToRad;
        double MRad = M * DegToRad;
        double MpRad = Mp * DegToRad;
        double FRad = F * DegToRad;

        // Perturbation terms using FMA chain
        double sinMp = Math.Sin(MpRad);
        double sin2DMp = Math.Sin((2.0 * DRad) - MpRad);
        double sin2D = Math.Sin(2.0 * DRad);
        double sin2Mp = Math.Sin(2.0 * MpRad);
        double sinM = Math.Sin(MRad);
        double sin2F = Math.Sin(2.0 * FRad);

        double dL = Math.FusedMultiplyAdd(109.154, sin2F,
                    Math.FusedMultiplyAdd(186.986, sinM,
                    Math.FusedMultiplyAdd(214.818, sin2Mp,
                    Math.FusedMultiplyAdd(658.314, sin2D,
                    Math.FusedMultiplyAdd(1274.242, sin2DMp,
                    6288.016 * sinMp)))));

        // Moon's true longitude
        double LMoon = Lp + (dL / 1000000.0);

        // Sun's mean longitude using FMA
        double LSun = NormalizeDegrees(Math.FusedMultiplyAdd(0.0003032, T2,
                      Math.FusedMultiplyAdd(36000.76983, T, 280.46646)));

        // Phase angle (elongation between Moon and Sun)
        double phaseAngle = NormalizeDegrees(LMoon - LSun) * DegToRad;

        // Lunar phase: 0.0 at new moon, 1.0 at full moon
        double phase = (1.0 - Math.Cos(phaseAngle)) / 2.0;

        return phase;
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
        // Lunar phase doesn't use price data, so Prime is a no-op
        // Each value would just recalculate based on the current time
    }
}