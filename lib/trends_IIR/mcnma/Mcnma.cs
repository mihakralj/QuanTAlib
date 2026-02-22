using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MCNMA: McNicholl EMA (Zero-Lag TEMA)
/// </summary>
/// <remarks>
/// Applies DEMA lag-cancellation to TEMA itself, using six cascaded EMA stages.
/// Three stages compute inner TEMA from source, three more compute outer TEMA
/// from the inner TEMA output. Result: 2×TEMA₁ - TEMA₂.
///
/// Dennis McNicholl, "Better Bollinger Bands," Futures Magazine, October 1998.
///
/// Calculation: <c>MCNMA = 2×TEMA(src,N) - TEMA(TEMA(src,N),N)</c>.
/// </remarks>
/// <seealso href="Mcnma.md">Detailed documentation</seealso>
/// <seealso href="mcnma.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Mcnma : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct EmaState(double Ema, double E, bool IsHot, bool IsCompensated)
    {
        public static EmaState New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };
    }

    private readonly double _alpha;
    private readonly double _decay;

    // Inner TEMA stages (source → EMA1 → EMA2 → EMA3)
    private EmaState _s1 = EmaState.New();
    private EmaState _s2 = EmaState.New();
    private EmaState _s3 = EmaState.New();
    // Outer TEMA stages (TEMA1 → EMA4 → EMA5 → EMA6)
    private EmaState _s4 = EmaState.New();
    private EmaState _s5 = EmaState.New();
    private EmaState _s6 = EmaState.New();

    private EmaState _ps1 = EmaState.New();
    private EmaState _ps2 = EmaState.New();
    private EmaState _ps3 = EmaState.New();
    private EmaState _ps4 = EmaState.New();
    private EmaState _ps5 = EmaState.New();
    private EmaState _ps6 = EmaState.New();

    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;
    private bool _isNew = true;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public bool IsNew => _isNew;
    public override bool IsHot => _s6.IsHot;

    public Mcnma(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        Name = $"Mcnma({period})";
        WarmupPeriod = period;
    }

    public Mcnma(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        _listener = Handle;
        source.Pub += _listener;
    }

    public Mcnma(double alpha)
    {
        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));
        }

        _alpha = alpha;
        _decay = 1.0 - alpha;
        Name = $"Mcnma(α={alpha:F4})";
        WarmupPeriod = (int)((2.0 / alpha) - 1.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        if (isNew)
        {
            _ps1 = _s1; _ps2 = _s2; _ps3 = _s3;
            _ps4 = _s4; _ps5 = _s5; _ps6 = _s6;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _s1 = _ps1; _s2 = _ps2; _s3 = _ps3;
            _s4 = _ps4; _s5 = _ps5; _s6 = _ps6;
            _lastValidValue = _p_lastValidValue;
        }

        double val = input.Value;
        if (double.IsFinite(val))
        {
            _lastValidValue = val;
        }
        else
        {
            val = _lastValidValue;
        }

        if (double.IsNaN(val))
        {
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Inner TEMA: 3 cascaded EMAs
        double c1 = Compute(val, _alpha, _decay, ref _s1);
        double c2 = Compute(c1, _alpha, _decay, ref _s2);
        double c3 = Compute(c2, _alpha, _decay, ref _s3);
        // TEMA1 = 3*c1 - 3*c2 + c3
        double tema1 = Math.FusedMultiplyAdd(3.0, c1, Math.FusedMultiplyAdd(-3.0, c2, c3));

        // Outer TEMA: 3 cascaded EMAs of TEMA1
        double c4 = Compute(tema1, _alpha, _decay, ref _s4);
        double c5 = Compute(c4, _alpha, _decay, ref _s5);
        double c6 = Compute(c5, _alpha, _decay, ref _s6);
        // TEMA2 = 3*c4 - 3*c5 + c6
        double tema2 = Math.FusedMultiplyAdd(3.0, c4, Math.FusedMultiplyAdd(-3.0, c5, c6));

        // MCNMA = 2*TEMA1 - TEMA2
        double result = Math.FusedMultiplyAdd(2.0, tema1, -tema2);
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        source.Times.CopyTo(tSpan);

        var sourceValues = source.Values;

        EmaState preBatch_s1 = _s1, preBatch_s2 = _s2, preBatch_s3 = _s3;
        EmaState preBatch_s4 = _s4, preBatch_s5 = _s5, preBatch_s6 = _s6;
        double preBatch_lastValid = _lastValidValue;

        EmaState s1 = _s1, s2 = _s2, s3 = _s3;
        EmaState s4 = _s4, s5 = _s5, s6 = _s6;
        double lastValid = _lastValidValue;
        double alpha = _alpha;
        double decay = _decay;

        for (int i = 0; i < len; i++)
        {
            double val = sourceValues[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            if (double.IsNaN(val))
            {
                vSpan[i] = double.NaN;
                continue;
            }

            double c1 = Compute(val, alpha, decay, ref s1);
            double c2 = Compute(c1, alpha, decay, ref s2);
            double c3 = Compute(c2, alpha, decay, ref s3);
            double tema1 = Math.FusedMultiplyAdd(3.0, c1, Math.FusedMultiplyAdd(-3.0, c2, c3));

            double c4 = Compute(tema1, alpha, decay, ref s4);
            double c5 = Compute(c4, alpha, decay, ref s5);
            double c6 = Compute(c5, alpha, decay, ref s6);
            double tema2 = Math.FusedMultiplyAdd(3.0, c4, Math.FusedMultiplyAdd(-3.0, c5, c6));

            vSpan[i] = Math.FusedMultiplyAdd(2.0, tema1, -tema2);
        }

        _s1 = s1; _s2 = s2; _s3 = s3;
        _s4 = s4; _s5 = s5; _s6 = s6;
        _lastValidValue = lastValid;

        _ps1 = preBatch_s1; _ps2 = preBatch_s2; _ps3 = preBatch_s3;
        _ps4 = preBatch_s4; _ps5 = preBatch_s5; _ps6 = preBatch_s6;
        _p_lastValidValue = preBatch_lastValid;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double alpha, double decay, ref EmaState state)
    {
        state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * input);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            if (!state.IsHot && state.E <= 0.05)
            {
                state.IsHot = true;
            }

            if (state.E <= 1e-10)
            {
                state.IsCompensated = true;
                result = state.Ema;
            }
            else
            {
                result = state.Ema / (1.0 - state.E);
            }
        }
        else
        {
            result = state.Ema;
        }

        return result;
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var mcnma = new Mcnma(period);
        return mcnma.Update(source);
    }

    public static TSeries Batch(TSeries source, double alpha)
    {
        var mcnma = new Mcnma(alpha);
        return mcnma.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        double alpha = 2.0 / (period + 1);
        Batch(source, output, alpha);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));
        }

        if (source.Length == 0)
        {
            return;
        }

        double decay = 1.0 - alpha;
        double lastValid = double.NaN;

        // 6 EMA stages inlined for maximum performance
        double e1 = 0, e2 = 0, e3 = 0, e4 = 0, e5 = 0, e6 = 0;
        double d1 = 1.0, d2 = 1.0, d3 = 1.0, d4 = 1.0, d5 = 1.0, d6 = 1.0;
        bool comp1 = false, comp2 = false, comp3 = false;
        bool comp4 = false, comp5 = false, comp6 = false;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

            // Stage 1: EMA of source
            e1 = Math.FusedMultiplyAdd(e1, decay, alpha * val);
            double c1;
            if (!comp1) { d1 *= decay; if (d1 <= 1e-10) { comp1 = true; c1 = e1; } else { c1 = e1 / (1.0 - d1); } }
            else { c1 = e1; }

            // Stage 2: EMA of c1
            e2 = Math.FusedMultiplyAdd(e2, decay, alpha * c1);
            double c2;
            if (!comp2) { d2 *= decay; if (d2 <= 1e-10) { comp2 = true; c2 = e2; } else { c2 = e2 / (1.0 - d2); } }
            else { c2 = e2; }

            // Stage 3: EMA of c2
            e3 = Math.FusedMultiplyAdd(e3, decay, alpha * c2);
            double c3;
            if (!comp3) { d3 *= decay; if (d3 <= 1e-10) { comp3 = true; c3 = e3; } else { c3 = e3 / (1.0 - d3); } }
            else { c3 = e3; }

            // TEMA1 = 3*c1 - 3*c2 + c3
            double tema1 = Math.FusedMultiplyAdd(3.0, c1, Math.FusedMultiplyAdd(-3.0, c2, c3));

            // Stage 4: EMA of TEMA1
            e4 = Math.FusedMultiplyAdd(e4, decay, alpha * tema1);
            double c4;
            if (!comp4) { d4 *= decay; if (d4 <= 1e-10) { comp4 = true; c4 = e4; } else { c4 = e4 / (1.0 - d4); } }
            else { c4 = e4; }

            // Stage 5: EMA of c4
            e5 = Math.FusedMultiplyAdd(e5, decay, alpha * c4);
            double c5;
            if (!comp5) { d5 *= decay; if (d5 <= 1e-10) { comp5 = true; c5 = e5; } else { c5 = e5 / (1.0 - d5); } }
            else { c5 = e5; }

            // Stage 6: EMA of c5
            e6 = Math.FusedMultiplyAdd(e6, decay, alpha * c5);
            double c6;
            if (!comp6) { d6 *= decay; if (d6 <= 1e-10) { comp6 = true; c6 = e6; } else { c6 = e6 / (1.0 - d6); } }
            else { c6 = e6; }

            // TEMA2 = 3*c4 - 3*c5 + c6
            double tema2 = Math.FusedMultiplyAdd(3.0, c4, Math.FusedMultiplyAdd(-3.0, c5, c6));

            // MCNMA = 2*TEMA1 - TEMA2
            output[i] = Math.FusedMultiplyAdd(2.0, tema1, -tema2);
        }
    }

    public static (TSeries Results, Mcnma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Mcnma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _s1 = EmaState.New(); _s2 = EmaState.New(); _s3 = EmaState.New();
        _s4 = EmaState.New(); _s5 = EmaState.New(); _s6 = EmaState.New();
        _ps1 = EmaState.New(); _ps2 = EmaState.New(); _ps3 = EmaState.New();
        _ps4 = EmaState.New(); _ps5 = EmaState.New(); _ps6 = EmaState.New();
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _listener != null)
        {
            _publisher.Pub -= _listener;
        }
        base.Dispose(disposing);
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);
}
