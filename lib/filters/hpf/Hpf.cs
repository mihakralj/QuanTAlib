using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Ehlers 2-pole High-Pass Filter (HPF).
/// Commonly used as the high-pass stage in the Roofing Filter.
/// </summary>
[SkipLocalsInit]
public sealed class Hpf : AbstractBase
{
    // Ehlers commonly uses 0.707 * 360/Len in the alpha expression (Roofing Filter HP stage).
    // 0.70710678... = sqrt(2)/2.
    private const double OmegaFactor = 0.70710678118654752440084436210485;

    private readonly int _length;
    private readonly double _c1; // (1 - a/2)^2
    private readonly double _c2; // 2*(1-a)
    private readonly double _c3; // (1-a)^2  (note: subtracted in recursion)
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    private State _state;
    private State _pState;

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public double Hp1;      // y[t-1]
        public double Hp2;      // y[t-2]
        public double Src1;     // x[t-1]
        public double Src2;     // x[t-2]
        public int Samples;     // number of valid (finite) samples seen
        public bool HasSrc;     // whether we have ever seen a finite source
    }

    public int Length => _length;

    public Hpf(int length = 40)
    {
        if (length < 2) throw new ArgumentOutOfRangeException(nameof(length), "Length must be at least 2.");

        _length = length;

        // ω = 0.707 * 2π/Len  (matches commonly published Roofing Filter HP stage)
        double omega = OmegaFactor * (2.0 * Math.PI / length);
        double cosW = Math.Cos(omega);
        double sinW = Math.Sin(omega);

        // cosW should not hit 0 for integer lengths with the 0.707 factor,
        // but guard anyway for pathological input.
        if (Math.Abs(cosW) < 1e-15)
            throw new ArgumentOutOfRangeException(nameof(length), "Length produces an unstable coefficient set (cos(ω)≈0).");

        double a = (cosW + sinW - 1.0) / cosW;
        double oneMinusA = 1.0 - a;

        double halfA = 0.5 * a;
        double t = 1.0 - halfA;

        _c1 = t * t;
        _c2 = 2.0 * oneMinusA;
        _c3 = oneMinusA * oneMinusA;

        Name = $"HPF({length})";
        WarmupPeriod = length; // heuristic; you can tighten this if you want
        Reset();
    }

    public Hpf(ITValuePublisher source, int length = 40) : this(length)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? _, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override bool IsHot => _state.Samples >= WarmupPeriod;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _state = default;
        _pState = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double v in source)
            Update(new TValue(DateTime.MinValue, v), isNew: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew) _pState = _state;
        else _state = _pState;

        double price = input.Value;

        // Missing/invalid measurement: carry forward last source (Pine nz-like).
        if (!double.IsFinite(price))
        {
            if (!_state.HasSrc)
            {
                Last = new TValue(input.Time, price);
                PubEvent(Last, isNew);
                return Last;
            }

            price = _state.Src1; // keep last finite source
        }

        // First finite sample
        if (!_state.HasSrc)
        {
            _state.HasSrc = true;
            _state.Src1 = price;
            _state.Src2 = price;
            _state.Hp1 = 0.0;
            _state.Hp2 = 0.0;
            _state.Samples = 1;

            Last = new TValue(input.Time, 0.0);
            PubEvent(Last, isNew);
            return Last;
        }

        // Second finite sample: we still don’t have x[t-2] history in a “meaningful” way.
        // Match the common approach of starting the recursion at the 3rd bar.
        if (_state.Samples == 1)
        {
            _state.Src2 = _state.Src1;
            _state.Src1 = price;
            _state.Samples = 2;

            Last = new TValue(input.Time, 0.0);
            PubEvent(Last, isNew);
            return Last;
        }

        double src1 = _state.Src1;
        double src2 = _state.Src2;
        double hp1  = _state.Hp1;
        double hp2  = _state.Hp2;

        // d2 = price - 2*src1 + src2  (use FMA)
        double d2 = Math.FusedMultiplyAdd(-2.0, src1, price + src2);

        // hp = c1*d2 + c2*hp1 - c3*hp2  (2 FMAs)
        double hp = Math.FusedMultiplyAdd(_c1, d2, _c2 * hp1);
        hp = Math.FusedMultiplyAdd(-_c3, hp2, hp);

        // shift state
        _state.Hp2 = hp1;
        _state.Hp1 = hp;
        _state.Src2 = src1;
        _state.Src1 = price;
        _state.Samples++;

        Last = new TValue(input.Time, hp);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        var output = new double[source.Count];

        Calculate(source.Values, output, _length);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
            result.Add(new TValue(times[i], output[i]));

        // To restore state, we could use the new Calculate overload that returns state,
        // or just accept that TSeries update re-runs logic.
        // But for perfromance, we want to capture state.
        // Let's use the explicit Calculate overload.

        Calculate(source.Values, output, _length, out var endState);

        _state = new State
        {
            Hp1 = endState.Hp1,
            Hp2 = endState.Hp2,
            Src1 = endState.Src1,
            Src2 = endState.Src2,
            Samples = endState.Samples,
            HasSrc = endState.HasSrc
        };
        _pState = _state;
        Last = new TValue(times[^1], output[^1]);

        return result;
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int length)
    {
        Calculate(source, output, length, out _);
    }

    /// <summary>
    /// Batch HPF. Returns end state so callers can restore streaming state without replay.
    /// NaN/Inf => carry-forward last finite source.
    /// Outputs 0 for the first two finite samples, then runs the 2-pole recursion.
    /// </summary>
    public static void Calculate(
        ReadOnlySpan<double> source,
        Span<double> output,
        int length,
        out (double Hp1, double Hp2, double Src1, double Src2, int Samples, bool HasSrc) state)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));

        if (source.Length == 0)
        {
            state = (0.0, 0.0, 0.0, 0.0, 0, false);
            return;
        }

        // coeffs
        const double omegaFactor = OmegaFactor;
        double omega = omegaFactor * (2.0 * Math.PI / length);
        double cosW = Math.Cos(omega);
        double sinW = Math.Sin(omega);

        if (Math.Abs(cosW) < 1e-15)
            throw new ArgumentOutOfRangeException(nameof(length), "Length produces an unstable coefficient set (cos(ω)≈0).");

        double a = (cosW + sinW - 1.0) / cosW;
        double oneMinusA = 1.0 - a;

        double t = 1.0 - 0.5 * a;
        double c1 = t * t;
        double c2 = 2.0 * oneMinusA;
        double c3 = oneMinusA * oneMinusA;

        double hp1 = 0.0, hp2 = 0.0;
        double src1 = 0.0, src2 = 0.0;
        int samples = 0;
        bool hasSrc = false;

        for (int i = 0; i < source.Length; i++)
        {
            double price = source[i];

            if (!double.IsFinite(price))
            {
                if (!hasSrc)
                {
                    output[i] = price;
                    continue;
                }
                price = src1;
            }

            if (!hasSrc)
            {
                hasSrc = true;
                src1 = price;
                src2 = price;
                hp1 = hp2 = 0.0;
                samples = 1;
                output[i] = 0.0;
                continue;
            }

            if (samples == 1)
            {
                src2 = src1;
                src1 = price;
                samples = 2;
                output[i] = 0.0;
                continue;
            }

            double d2 = Math.FusedMultiplyAdd(-2.0, src1, price + src2);
            double hp = Math.FusedMultiplyAdd(c1, d2, c2 * hp1);
            hp = Math.FusedMultiplyAdd(-c3, hp2, hp);

            output[i] = hp;

            hp2 = hp1; hp1 = hp;
            src2 = src1; src1 = price;
            samples++;
        }

        state = (hp1, hp2, src1, src2, samples, hasSrc);
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
