using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DECO: Decycler Oscillator
/// </summary>
/// <remarks>
/// Ehlers' Decycler Oscillator isolates market cycles by computing the difference
/// between two 2-pole Butterworth high-pass filters with different cutoff periods.
/// The shorter HP filter passes more cycle content; the longer HP filter passes less.
/// Their difference reveals the intermediate-frequency band where tradable cycles live.
///
/// Formula (Ehlers, TASC September 2015, Equation 4-2):
/// <code>
/// α = (cos(0.707 × 360/period) + sin(0.707 × 360/period) - 1) / cos(0.707 × 360/period)
/// HP[n] = (1 - α/2)² × (x[n] - 2×x[n-1] + x[n-2]) + 2×(1-α)×HP[n-1] - (1-α)²×HP[n-2]
/// DECO = HP_long - HP_short
/// </code>
///
/// The 0.707 factor (1/√2) places the filter at the -3 dB point of the Butterworth response.
///
/// References:
///   John F. Ehlers, "Decyclers", Technical Analysis of Stocks &amp; Commodities, September 2015
///   John F. Ehlers, "Cycle Analytics for Traders", Wiley, 2013, Chapter 4
/// </remarks>
[SkipLocalsInit]
public sealed class Deco : AbstractBase
{
    private readonly int _shortPeriod;
    private readonly int _longPeriod;

    // Precomputed HP filter coefficients for short-period filter
    private readonly double _a1Short; // (1 - α/2)²
    private readonly double _b1Short; // 2 × (1 - α)
    private readonly double _c1Short; // -(1 - α)²

    // Precomputed HP filter coefficients for long-period filter
    private readonly double _a1Long;
    private readonly double _b1Long;
    private readonly double _c1Long;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double HpShort1,
        double HpShort2,
        double HpLong1,
        double HpLong2,
        double Price1,
        double Price2,
        int Count,
        double LastValidValue);

    private State _s;
    private State _ps;

    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>Short-period HP cutoff.</summary>
    public int ShortPeriod => _shortPeriod;

    /// <summary>Long-period HP cutoff.</summary>
    public int LongPeriod => _longPeriod;

    /// <summary>
    /// Creates a Decycler Oscillator with specified cutoff periods.
    /// </summary>
    /// <param name="shortPeriod">Short HP cutoff period (must be &gt; 0).</param>
    /// <param name="longPeriod">Long HP cutoff period (must be &gt; shortPeriod).</param>
    public Deco(int shortPeriod = 30, int longPeriod = 60)
    {
        if (shortPeriod <= 0)
        {
            throw new ArgumentException("Short period must be greater than 0.", nameof(shortPeriod));
        }
        if (longPeriod <= shortPeriod)
        {
            throw new ArgumentException("Long period must be greater than short period.", nameof(longPeriod));
        }

        _shortPeriod = shortPeriod;
        _longPeriod = longPeriod;

        // Precompute Butterworth HP coefficients: α = (cos(0.707×360/p) + sin(0.707×360/p) - 1) / cos(0.707×360/p)
        double rad = 0.707 * 2.0 * Math.PI; // 0.707 × 360° in radians

        double argShort = rad / shortPeriod;
        double alphaShort = (Math.Cos(argShort) + Math.Sin(argShort) - 1.0) / Math.Cos(argShort);
        double oneMinusAlphaHalfShort = 1.0 - alphaShort * 0.5;
        double oneMinusAlphaShort = 1.0 - alphaShort;
        _a1Short = oneMinusAlphaHalfShort * oneMinusAlphaHalfShort;
        _b1Short = 2.0 * oneMinusAlphaShort;
        _c1Short = -(oneMinusAlphaShort * oneMinusAlphaShort);

        double argLong = rad / longPeriod;
        double alphaLong = (Math.Cos(argLong) + Math.Sin(argLong) - 1.0) / Math.Cos(argLong);
        double oneMinusAlphaHalfLong = 1.0 - alphaLong * 0.5;
        double oneMinusAlphaLong = 1.0 - alphaLong;
        _a1Long = oneMinusAlphaHalfLong * oneMinusAlphaHalfLong;
        _b1Long = 2.0 * oneMinusAlphaLong;
        _c1Long = -(oneMinusAlphaLong * oneMinusAlphaLong);

        Name = $"Deco({shortPeriod},{longPeriod})";
        WarmupPeriod = longPeriod;

        _s = default;
        _ps = default;
    }

    /// <summary>
    /// Creates a chained Decycler Oscillator.
    /// </summary>
    public Deco(ITValuePublisher source, int shortPeriod = 30, int longPeriod = 60) : this(shortPeriod, longPeriod)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew) { _ps = _s; } else { _s = _ps; }
        var s = _s;

        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(s.LastValidValue) ? s.LastValidValue : 0.0;
        }
        else
        {
            s = s with { LastValidValue = value };
        }

        double hpShort, hpLong;

        if (s.Count < 2)
        {
            // Not enough history for 2-pole HP — output zero
            hpShort = 0.0;
            hpLong = 0.0;
            s = s with
            {
                HpShort1 = 0.0,
                HpShort2 = 0.0,
                HpLong1 = 0.0,
                HpLong2 = 0.0,
            };
        }
        else
        {
            // HP[n] = a1*(x[n] - 2*x[n-1] + x[n-2]) + b1*HP[n-1] + c1*HP[n-2]
            double diff = value - 2.0 * s.Price1 + s.Price2;
            hpShort = Math.FusedMultiplyAdd(_a1Short, diff, Math.FusedMultiplyAdd(_b1Short, s.HpShort1, _c1Short * s.HpShort2));
            hpLong = Math.FusedMultiplyAdd(_a1Long, diff, Math.FusedMultiplyAdd(_b1Long, s.HpLong1, _c1Long * s.HpLong2));

            s = s with
            {
                HpShort2 = s.HpShort1,
                HpShort1 = hpShort,
                HpLong2 = s.HpLong1,
                HpLong1 = hpLong,
            };
        }

        // DECO = HP_long - HP_short (long-period HP passes fewer cycles → more trend-like)
        double deco = hpLong - hpShort;

        _s = s with { Price2 = s.Price1, Price1 = value, Count = s.Count + 1 };

        Last = new TValue(input.Time, deco);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) { return []; }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _shortPeriod, _longPeriod);
        source.Times.CopyTo(tSpan);

        // Replay to set internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    public override void Reset()
    {
        _s = default;
        _ps = default;
        Last = default;
    }

    /// <summary>
    /// Calculates DECO for an entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int shortPeriod = 30, int longPeriod = 60)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, shortPeriod, longPeriod);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Span-based batch DECO calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int shortPeriod = 30, int longPeriod = 60)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (shortPeriod <= 0)
        {
            throw new ArgumentException("Short period must be greater than 0.", nameof(shortPeriod));
        }
        if (longPeriod <= shortPeriod)
        {
            throw new ArgumentException("Long period must be greater than short period.", nameof(longPeriod));
        }

        int len = source.Length;
        if (len == 0) { return; }

        double rad = 0.707 * 2.0 * Math.PI;

        double argShort = rad / shortPeriod;
        double alphaShort = (Math.Cos(argShort) + Math.Sin(argShort) - 1.0) / Math.Cos(argShort);
        double omahShort = 1.0 - alphaShort * 0.5;
        double omaShort = 1.0 - alphaShort;
        double a1S = omahShort * omahShort;
        double b1S = 2.0 * omaShort;
        double c1S = -(omaShort * omaShort);

        double argLong = rad / longPeriod;
        double alphaLong = (Math.Cos(argLong) + Math.Sin(argLong) - 1.0) / Math.Cos(argLong);
        double omahLong = 1.0 - alphaLong * 0.5;
        double omaLong = 1.0 - alphaLong;
        double a1L = omahLong * omahLong;
        double b1L = 2.0 * omaLong;
        double c1L = -(omaLong * omaLong);

        double hpS1 = 0, hpS2 = 0, hpL1 = 0, hpL2 = 0;
        double price1 = 0, price2 = 0;
        double lastValid = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val)) { val = lastValid; } else { lastValid = val; }

            if (i < 2)
            {
                output[i] = 0.0;
            }
            else
            {
                double diff = val - 2.0 * price1 + price2;
                double hpS = Math.FusedMultiplyAdd(a1S, diff, Math.FusedMultiplyAdd(b1S, hpS1, c1S * hpS2));
                double hpL = Math.FusedMultiplyAdd(a1L, diff, Math.FusedMultiplyAdd(b1L, hpL1, c1L * hpL2));
                output[i] = hpL - hpS;
                hpS2 = hpS1; hpS1 = hpS;
                hpL2 = hpL1; hpL1 = hpL;
            }

            price2 = price1;
            price1 = val;
        }
    }

    /// <summary>
    /// Calculates DECO and returns both results and a primed indicator.
    /// </summary>
    public static (TSeries Results, Deco Indicator) Calculate(TSeries source,
        int shortPeriod = 30, int longPeriod = 60)
    {
        var ind = new Deco(shortPeriod, longPeriod);
        var results = ind.Update(source);
        return (results, ind);
    }

}
