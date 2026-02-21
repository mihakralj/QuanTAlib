using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HOLT: Holt Exponential Moving Average (Double Exponential Smoothing)
/// </summary>
/// <remarks>
/// Holt's (1957) double exponential smoothing tracks both level and trend,
/// producing a 1-step-ahead forecast that adapts to trending data.
///
/// Calculation:
/// <c>L_t = α·y_t + (1-α)·(L_{t-1} + B_{t-1})</c> (Level)
/// <c>B_t = γ·(L_t - L_{t-1}) + (1-γ)·B_{t-1}</c> (Trend)
/// <c>HOLT_t = L_t + B_t</c> (1-step-ahead forecast)
///
/// When gamma=0, degenerates to standard EMA (no trend correction).
/// When gamma=alpha, provides balanced level/trend tracking.
/// </remarks>
/// <seealso href="Holt.md">Detailed documentation</seealso>
/// <seealso href="holt.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Holt : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Level, double Trend, int Count, bool IsHot, bool Initialized)
    {
        public static State New() => new() { Level = 0, Trend = 0, Count = 0, IsHot = false, Initialized = false };
    }

    private readonly double _alpha;
    private readonly double _decay;
    private readonly double _gamma;
    private readonly double _gammaDecay;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;

    /// <summary>
    /// Creates Holt with specified period and trend smoothing factor.
    /// Alpha = 2 / (period + 1). Gamma defaults to alpha when 0.
    /// </summary>
    /// <param name="period">Smoothing period (must be &gt; 0)</param>
    /// <param name="gamma">Trend smoothing factor [0..1]. 0 = auto (uses alpha)</param>
    public Holt(int period, double gamma = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        if (gamma < 0 || gamma > 1)
        {
            throw new ArgumentException("Gamma must be between 0 and 1", nameof(gamma));
        }

        _alpha = 2.0 / (period + 1.0);
        _decay = 1.0 - _alpha;
        _gamma = gamma > 0 ? gamma : _alpha;
        _gammaDecay = 1.0 - _gamma;

        Name = gamma > 0 ? $"Holt({period},{gamma:F2})" : $"Holt({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates Holt with specified source and parameters.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Holt(ITValuePublisher source, int period, double gamma = 0) : this(period, gamma)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates Holt from a TSeries source with specified parameters.
    /// Primes from history and subscribes to source.Pub event.
    /// </summary>
    public Holt(TSeries source, int period, double gamma = 0) : this(period, gamma)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    /// <summary>
    /// True when the Holt indicator has received enough data for valid output.
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
            CalculateCore(source, tempOutput, _alpha, _decay, _gamma, _gammaDecay, WarmupPeriod, ref _state, ref _lastValidValue);
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
        val = Compute(val, _alpha, _decay, _gamma, _gammaDecay, WarmupPeriod, ref _state);
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

        CalculateCore(sourceValues, vSpan, _alpha, _decay, _gamma, _gammaDecay, WarmupPeriod, ref state, ref lastValidValue);

        _state = state;
        _lastValidValue = lastValidValue;
        sourceTimes.CopyTo(tSpan);

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core computation: Holt double exponential smoothing.
    /// Level and trend equations use FMA for precision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double alpha, double decay, double gamma, double gammaDecay, int warmup, ref State state)
    {
        if (!state.Initialized)
        {
            // First bar: initialize level to input, trend to 0
            state.Level = input;
            state.Trend = 0;
            state.Initialized = true;
            state.Count = 1;
            if (warmup <= 1)
            {
                state.IsHot = true;
            }
            return input;
        }

        double prevLevel = state.Level;

        // Level: alpha * input + (1 - alpha) * (prevLevel + trend)
        // = FMA(alpha, input, decay * (prevLevel + trend))
        state.Level = Math.FusedMultiplyAdd(alpha, input, decay * (prevLevel + state.Trend));

        // Trend: gamma * (level - prevLevel) + (1 - gamma) * trend
        // = FMA(gamma, level - prevLevel, gammaDecay * trend)
        state.Trend = Math.FusedMultiplyAdd(gamma, state.Level - prevLevel, gammaDecay * state.Trend);

        state.Count++;
        if (!state.IsHot && state.Count >= warmup)
        {
            state.IsHot = true;
        }

        // Output: level + trend (1-step-ahead forecast)
        return state.Level + state.Trend;
    }

    /// <summary>
    /// Core batch calculation with NaN handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output,
        double alpha, double decay, double gamma, double gammaDecay,
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

            if (!state.Initialized)
            {
                state.Level = val;
                state.Trend = 0;
                state.Initialized = true;
                state.Count = 1;
                if (warmup <= 1)
                {
                    state.IsHot = true;
                }
                Unsafe.Add(ref outRef, i) = val;
                continue;
            }

            double prevLevel = state.Level;
            state.Level = Math.FusedMultiplyAdd(alpha, val, decay * (prevLevel + state.Trend));
            state.Trend = Math.FusedMultiplyAdd(gamma, state.Level - prevLevel, gammaDecay * state.Trend);

            state.Count++;
            if (!state.IsHot && state.Count >= warmup)
            {
                state.IsHot = true;
            }

            Unsafe.Add(ref outRef, i) = state.Level + state.Trend;
        }
    }

    /// <summary>
    /// Calculates Holt for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double gamma = 0)
    {
        var holt = new Holt(period, gamma);
        return holt.Update(source);
    }

    /// <summary>
    /// Calculates Holt in-place using pre-allocated output span. Zero-allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double gamma = 0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        if (gamma < 0 || gamma > 1)
        {
            throw new ArgumentException("Gamma must be between 0 and 1", nameof(gamma));
        }

        if (source.Length == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1.0);
        double decay = 1.0 - alpha;
        double g = gamma > 0 ? gamma : alpha;
        double gDecay = 1.0 - g;

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

        CalculateCore(source, output, alpha, decay, g, gDecay, period, ref state, ref lastValid);
    }

    /// <summary>
    /// Runs a high-performance batch and returns a hot Holt instance.
    /// </summary>
    public static (TSeries Results, Holt Indicator) Calculate(TSeries source, int period, double gamma = 0)
    {
        var holt = new Holt(period, gamma);
        TSeries results = holt.Update(source);
        return (results, holt);
    }

    /// <summary>
    /// Resets the Holt filter state.
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
