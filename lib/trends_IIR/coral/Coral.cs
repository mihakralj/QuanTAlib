using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CORAL: Coral Trend Filter
/// </summary>
/// <remarks>
/// Six cascaded EMAs with polynomial combination using "Constant D" parameter.
/// Produces a smooth, low-lag trend line by chaining 6 EMA passes and linearly
/// combining stages 3–6 with polynomial coefficients derived from cd.
///
/// Calculation:
/// <c>di = (period-1)/2 + 1</c>, <c>α = 2/(di+1)</c>, cascade 6 EMAs,
/// <c>bfr = -cd³·i6 + c3·i5 + c4·i4 + c5·i3</c>.
/// Unity DC gain: c3 + c4 + c5 + (-cd³) = 1.
/// </remarks>
/// <seealso href="Coral.md">Detailed documentation</seealso>
/// <seealso href="coral.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Coral : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double I1, double I2, double I3, double I4, double I5, double I6, int Count, bool IsHot)
    {
        public static State New() => new() { I1 = 0, I2 = 0, I3 = 0, I4 = 0, I5 = 0, I6 = 0, Count = 0, IsHot = false };
    }

    private readonly double _alpha;
    private readonly double _decay;
    private readonly double _cd3;
    private readonly double _c3;
    private readonly double _c4;
    private readonly double _c5;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;

    /// <summary>
    /// Creates Coral with specified period and Constant D.
    /// Alpha = 2 / (di + 1) where di = (period - 1) / 2 + 1.
    /// </summary>
    /// <param name="period">Smoothing period (must be &gt; 0)</param>
    /// <param name="cd">Constant D controlling polynomial weights (must be in [0, 1], default 0.4)</param>
    public Coral(int period, double cd = 0.4)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        if (cd < 0 || cd > 1)
        {
            throw new ArgumentException("Constant D must be between 0 and 1", nameof(cd));
        }

        double di = ((period - 1.0) / 2.0) + 1.0;
        _alpha = 2.0 / (di + 1.0);
        _decay = 1.0 - _alpha;

        double cd2 = cd * cd;
        _cd3 = cd2 * cd;
        _c3 = 3.0 * (cd2 + _cd3);
        _c4 = -3.0 * ((2.0 * cd2) + cd + _cd3);
        _c5 = (3.0 * cd) + 1.0 + _cd3 + (3.0 * cd2);

        Name = $"Coral({period},{cd:F2})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates Coral with specified source and parameters.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Coral(ITValuePublisher source, int period, double cd = 0.4) : this(period, cd)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates Coral from a TSeries source with specified parameters.
    /// Primes from history and subscribes to source.Pub event.
    /// </summary>
    public Coral(TSeries source, int period, double cd = 0.4) : this(period, cd)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    /// <summary>
    /// True when the Coral filter has received enough data for valid output.
    /// </summary>
    public override bool IsHot => _state.IsHot;

    private const int StackAllocThreshold = 512;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _state = State.New();
        _p_state = State.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        int len = source.Length;

        bool foundValid = false;
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                _lastValidValue = source[k];
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            Last = new TValue(DateTime.MinValue, double.NaN);
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
            return;
        }

        double[]? rented = len > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> tempOutput = rented != null
            ? rented.AsSpan(0, len)
            : stackalloc double[len];

        try
        {
            CalculateCore(source, tempOutput, _alpha, _decay, _cd3, _c3, _c4, _c5, WarmupPeriod, ref _state, ref _lastValidValue);
            Last = new TValue(DateTime.MinValue, tempOutput[len - 1]);
            _p_state = _state;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state = _p_state;
            _lastValidValue = _p_lastValidValue;
        }

        double val = GetValidValue(input.Value);
        val = Compute(val, _alpha, _decay, _cd3, _c3, _c4, _c5, WarmupPeriod, ref _state);
        Last = new TValue(input.Time, val);
        PubEvent(Last, isNew);
        return Last;
    }

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
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        State state = _state;
        double lastValidValue = _lastValidValue;

        CalculateCore(sourceValues, vSpan, _alpha, _decay, _cd3, _c3, _c4, _c5, WarmupPeriod, ref state, ref lastValidValue);

        _state = state;
        _lastValidValue = lastValidValue;
        sourceTimes.CopyTo(tSpan);

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core computation: 6 cascaded EMAs + polynomial combination.
    /// All EMA stages use FMA for precision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double alpha, double decay, double cd3, double c3, double c4, double c5, int warmup, ref State state)
    {
        // 6 cascaded EMAs using FMA: ema = decay * ema + alpha * input
        state.I1 = Math.FusedMultiplyAdd(state.I1, decay, alpha * input);
        state.I2 = Math.FusedMultiplyAdd(state.I2, decay, alpha * state.I1);
        state.I3 = Math.FusedMultiplyAdd(state.I3, decay, alpha * state.I2);
        state.I4 = Math.FusedMultiplyAdd(state.I4, decay, alpha * state.I3);
        state.I5 = Math.FusedMultiplyAdd(state.I5, decay, alpha * state.I4);
        state.I6 = Math.FusedMultiplyAdd(state.I6, decay, alpha * state.I5);

        state.Count++;
        if (!state.IsHot && state.Count >= warmup)
        {
            state.IsHot = true;
        }

        // Polynomial combination of stages 3-6 using nested FMA:
        // bfr = -cd³·i6 + c3·i5 + c4·i4 + c5·i3
        return Math.FusedMultiplyAdd(-cd3, state.I6,
            Math.FusedMultiplyAdd(c3, state.I5,
                Math.FusedMultiplyAdd(c4, state.I4, c5 * state.I3)));
    }

    /// <summary>
    /// Core batch calculation with NaN handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output,
        double alpha, double decay, double cd3, double c3, double c4, double c5,
        int warmup, ref State state, ref double lastValidValue)
    {
        int len = source.Length;
        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        for (int i = 0; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            if (!double.IsFinite(val))
            {
                val = lastValidValue;
            }
            else
            {
                lastValidValue = val;
            }

            // 6 cascaded EMAs
            state.I1 = Math.FusedMultiplyAdd(state.I1, decay, alpha * val);
            state.I2 = Math.FusedMultiplyAdd(state.I2, decay, alpha * state.I1);
            state.I3 = Math.FusedMultiplyAdd(state.I3, decay, alpha * state.I2);
            state.I4 = Math.FusedMultiplyAdd(state.I4, decay, alpha * state.I3);
            state.I5 = Math.FusedMultiplyAdd(state.I5, decay, alpha * state.I4);
            state.I6 = Math.FusedMultiplyAdd(state.I6, decay, alpha * state.I5);

            state.Count++;
            if (!state.IsHot && state.Count >= warmup)
            {
                state.IsHot = true;
            }

            // Polynomial combination
            Unsafe.Add(ref outRef, i) = Math.FusedMultiplyAdd(-cd3, state.I6,
                Math.FusedMultiplyAdd(c3, state.I5,
                    Math.FusedMultiplyAdd(c4, state.I4, c5 * state.I3)));
        }
    }

    /// <summary>
    /// Calculates Coral for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double cd = 0.4)
    {
        var coral = new Coral(period, cd);
        return coral.Update(source);
    }

    /// <summary>
    /// Calculates Coral in-place using pre-allocated output span. Zero-allocation.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">Smoothing period (must be &gt; 0)</param>
    /// <param name="cd">Constant D (must be in [0, 1], default 0.4)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double cd = 0.4)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        if (cd < 0 || cd > 1)
        {
            throw new ArgumentException("Constant D must be between 0 and 1", nameof(cd));
        }

        if (source.Length == 0)
        {
            return;
        }

        double di = ((period - 1.0) / 2.0) + 1.0;
        double alpha = 2.0 / (di + 1.0);
        double decay = 1.0 - alpha;

        double cd2 = cd * cd;
        double cd3 = cd2 * cd;
        double c3 = 3.0 * (cd2 + cd3);
        double c4 = -3.0 * ((2.0 * cd2) + cd + cd3);
        double c5 = (3.0 * cd) + 1.0 + cd3 + (3.0 * cd2);

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

        CalculateCore(source, output, alpha, decay, cd3, c3, c4, c5, period, ref state, ref lastValid);
    }

    /// <summary>
    /// Runs a high-performance batch and returns a hot Coral instance.
    /// </summary>
    public static (TSeries Results, Coral Indicator) Calculate(TSeries source, int period, double cd = 0.4)
    {
        var coral = new Coral(period, cd);
        TSeries results = coral.Update(source);
        return (results, coral);
    }

    /// <summary>
    /// Resets the Coral filter state.
    /// </summary>
    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}
