using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// REVERSEEMA: Ehlers Reverse EMA
/// </summary>
/// <remarks>
/// Removes EMA lag via 8-stage cascaded Z-transform inversion of exponential smoothing.
/// John F. Ehlers (2017) — applies successive reverse stages with exponentially
/// increasing powers of the decay factor to progressively extract the lag component,
/// then subtracts it from the compensated EMA.
///
/// Calculation: <c>Signal = EMA - α × RE8</c> where each stage
/// <c>RE_k[n] = cc^(2^(k-1)) × RE_{k-1}[n] + RE_{k-1}[n-1]</c>
/// </remarks>
/// <seealso href="ReverseEma.md">Detailed documentation</seealso>
/// <seealso href="reverseema.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class ReverseEma : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Ema, double E, bool IsHot, bool IsCompensated,
        double Re1, double Re2, double Re3, double Re4,
        double Re5, double Re6, double Re7, double Re8,
        double PrevEma, double PrevRe1, double PrevRe2, double PrevRe3,
        double PrevRe4, double PrevRe5, double PrevRe6, double PrevRe7)
    {
        public static State New() => new()
        {
            Ema = 0,
            E = 1.0,
            IsHot = false,
            IsCompensated = false,
            Re1 = 0,
            Re2 = 0,
            Re3 = 0,
            Re4 = 0,
            Re5 = 0,
            Re6 = 0,
            Re7 = 0,
            Re8 = 0,
            PrevEma = 0,
            PrevRe1 = 0,
            PrevRe2 = 0,
            PrevRe3 = 0,
            PrevRe4 = 0,
            PrevRe5 = 0,
            PrevRe6 = 0,
            PrevRe7 = 0
        };
    }

    private readonly double _alpha;
    private readonly double _decay;
    // Precomputed powers: cc^1, cc^2, cc^4, cc^8, cc^16, cc^32, cc^64, cc^128
    private readonly double _cc1;
    private readonly double _cc2;
    private readonly double _cc4;
    private readonly double _cc8;
    private readonly double _cc16;
    private readonly double _cc32;
    private readonly double _cc64;
    private readonly double _cc128;

    private State _s = State.New();
    private State _ps = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;

    private const double COVERAGE_THRESHOLD = 0.05;
    private const double COMPENSATOR_THRESHOLD = 1e-10;
    private const int StackallocThreshold = 1024;

    /// <summary>
    /// Creates ReverseEma with specified period.
    /// Alpha = 2 / (period + 1)
    /// </summary>
    /// <param name="period">Period for the base EMA (must be &gt; 0)</param>
    public ReverseEma(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;

        // Precompute powers of decay for the 8 reverse stages
        _cc1 = _decay;                 // cc^1
        _cc2 = _cc1 * _cc1;           // cc^2
        _cc4 = _cc2 * _cc2;           // cc^4
        _cc8 = _cc4 * _cc4;           // cc^8
        _cc16 = _cc8 * _cc8;          // cc^16
        _cc32 = _cc16 * _cc16;        // cc^32
        _cc64 = _cc32 * _cc32;        // cc^64
        _cc128 = _cc64 * _cc64;       // cc^128

        Name = $"ReverseEma({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates ReverseEma with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    public ReverseEma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates ReverseEma with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public ReverseEma(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    /// <inheritdoc/>
    public override bool IsHot => _s.IsHot;

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        // Find first valid value
        for (int k = 0; k < source.Length; k++)
        {
            if (double.IsFinite(source[k]))
            {
                _lastValidValue = source[k];
                break;
            }
        }

        int len = source.Length;
        double[]? rented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> temp = rented != null ? rented.AsSpan(0, len) : stackalloc double[len];

        try
        {
            CalculateCore(source, temp, _alpha, _decay,
                _cc1, _cc2, _cc4, _cc8, _cc16, _cc32, _cc64, _cc128,
                ref _s, ref _lastValidValue);

            Last = new TValue(DateTime.MinValue, temp[len - 1]);
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _s = _ps;
            _lastValidValue = _p_lastValidValue;
        }

        var s = _s;

        double val = GetValidValue(input.Value);
        double result = Compute(val, _alpha, _decay,
            _cc1, _cc2, _cc4, _cc8, _cc16, _cc32, _cc64, _cc128,
            ref s);

        _s = s;
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        CalculateCore(source.Values, vSpan, _alpha, _decay,
            _cc1, _cc2, _cc4, _cc8, _cc16, _cc32, _cc64, _cc128,
            ref _s, ref _lastValidValue);

        source.Times.CopyTo(tSpan);

        _ps = _s;
        _p_lastValidValue = _lastValidValue;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming computation: EMA step + 8-stage cascaded reverse + signal extraction.
    /// O(1) per bar, zero allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double alpha, double decay,
        double cc1, double cc2, double cc4, double cc8,
        double cc16, double cc32, double cc64, double cc128,
        ref State s)
    {
        // --- Forward EMA with warmup compensation ---
        s.Ema = Math.FusedMultiplyAdd(s.Ema, decay, alpha * input);

        double emaVal;
        if (!s.IsCompensated)
        {
            s.E *= decay;
            if (!s.IsHot && s.E <= COVERAGE_THRESHOLD)
            {
                s.IsHot = true;
            }
            if (s.E <= COMPENSATOR_THRESHOLD)
            {
                s.IsCompensated = true;
                emaVal = s.Ema;
            }
            else
            {
                emaVal = s.Ema / (1.0 - s.E);
            }
        }
        else
        {
            emaVal = s.Ema;
        }

        // --- 8-stage cascaded reverse EMA ---
        // RE_k[n] = cc^(2^(k-1)) * input_k[n] + input_k[n-1]
        // Stage 1 uses emaVal as input
        double re1 = Math.FusedMultiplyAdd(cc1, emaVal, s.PrevEma);
        double re2 = Math.FusedMultiplyAdd(cc2, re1, s.PrevRe1);
        double re3 = Math.FusedMultiplyAdd(cc4, re2, s.PrevRe2);
        double re4 = Math.FusedMultiplyAdd(cc8, re3, s.PrevRe3);
        double re5 = Math.FusedMultiplyAdd(cc16, re4, s.PrevRe4);
        double re6 = Math.FusedMultiplyAdd(cc32, re5, s.PrevRe5);
        double re7 = Math.FusedMultiplyAdd(cc64, re6, s.PrevRe6);
        double re8 = Math.FusedMultiplyAdd(cc128, re7, s.PrevRe7);

        // Shift current → previous for next bar
        s.PrevEma = emaVal;
        s.PrevRe1 = re1;
        s.PrevRe2 = re2;
        s.PrevRe3 = re3;
        s.PrevRe4 = re4;
        s.PrevRe5 = re5;
        s.PrevRe6 = re6;
        s.PrevRe7 = re7;

        s.Re1 = re1;
        s.Re2 = re2;
        s.Re3 = re3;
        s.Re4 = re4;
        s.Re5 = re5;
        s.Re6 = re6;
        s.Re7 = re7;
        s.Re8 = re8;

        // Signal = EMA - alpha * RE8
        return Math.FusedMultiplyAdd(-alpha, re8, emaVal);
    }

    /// <summary>
    /// Core batch calculation with NaN handling and warmup compensation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output,
        double alpha, double decay,
        double cc1, double cc2, double cc4, double cc8,
        double cc16, double cc32, double cc64, double cc128,
        ref State s, ref double lastValidValue)
    {
        int len = source.Length;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValidValue = val;
            }
            else
            {
                val = lastValidValue;
            }

            output[i] = Compute(val, alpha, decay,
                cc1, cc2, cc4, cc8, cc16, cc32, cc64, cc128,
                ref s);
        }
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new ReverseEma(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        if (source.Length == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1);
        double decay = 1.0 - alpha;
        double c1 = decay;
        double c2 = c1 * c1;
        double c4 = c2 * c2;
        double c8 = c4 * c4;
        double c16 = c8 * c8;
        double c32 = c16 * c16;
        double c64 = c32 * c32;
        double c128 = c64 * c64;

        var state = State.New();
        double lastValid = 0;

        bool foundValid = false;
        for (int k = 0; k < source.Length; k++)
        {
            if (double.IsFinite(source[k]))
            {
                lastValid = source[k];
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            output.Fill(double.NaN);
            return;
        }

        CalculateCore(source, output, alpha, decay, c1, c2, c4, c8, c16, c32, c64, c128,
            ref state, ref lastValid);
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, ReverseEma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new ReverseEma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}
