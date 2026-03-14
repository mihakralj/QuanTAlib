using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// REMA: Regularized Exponential Moving Average
/// </summary>
/// <remarks>
/// Combines EMA smoothing with regularization term penalizing trend direction changes.
/// Lambda controls blend: 0 = pure momentum, 1 = standard EMA.
///
/// Calculation: <c>REMA = λ×(EMA_comp - REG_comp) + REG_comp</c>.
/// </remarks>
/// <seealso href="Rema.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Rema : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Rema, double PrevRema, double E, bool IsHot, bool IsCompensated, bool IsInitialized)
    {
        public static State New() => new()
        {
            Rema = 0,
            PrevRema = 0,
            E = 1.0,
            IsHot = false,
            IsCompensated = false,
            IsInitialized = false
        };
    }

    private readonly double _alpha;
    private readonly double _decay;
    private readonly double _lambda;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;

    private const double COVERAGE_THRESHOLD = 0.05;
    private const double COMPENSATOR_THRESHOLD = 1e-10;

    /// <summary>
    /// Creates REMA with specified period and lambda.
    /// Alpha = 2 / (period + 1)
    /// </summary>
    /// <param name="period">Period for EMA calculation (must be > 0)</param>
    /// <param name="lambda">Regularization parameter (0-1). 0 = max regularization, 1 = standard EMA</param>
    public Rema(int period, double lambda = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        if (lambda < 0.0 || lambda > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), "Lambda must be between 0 and 1");
        }

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        _lambda = lambda;
        Name = $"Rema({period},{lambda:F2})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates REMA with specified source, period, and lambda.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Rema(ITValuePublisher source, int period, double lambda = 0.5) : this(period, lambda)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates REMA from TSeries source with auto-subscription.
    /// </summary>
    public Rema(TSeries source, int period, double lambda = 0.5) : this(period, lambda)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }
    public override bool IsHot => _state.IsHot;

    private const int StackAllocThreshold = 512;
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
            CalculateCore(source, tempOutput, _alpha, _lambda, ref _state, ref _lastValidValue);
            double result = tempOutput[len - 1];
            Last = new TValue(DateTime.MinValue, result);
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
        val = Compute(val, _alpha, _decay, _lambda, ref _state);
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

        CalculateCore(sourceValues, vSpan, _alpha, _lambda, ref state, ref lastValidValue);

        _state = state;
        _lastValidValue = lastValidValue;

        sourceTimes.CopyTo(tSpan);

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core REMA computation with bias compensation.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double alpha, double decay, double lambda, ref State state)
    {
        double result;

        if (!state.IsInitialized)
        {
            // First value: initialize
            state.Rema = input;
            state.PrevRema = input;
            state.IsInitialized = true;
            state.E *= decay;

            if (state.E <= COVERAGE_THRESHOLD)
            {
                state.IsHot = true;
            }

            result = input;
        }
        else
        {
            double prevRema = state.Rema;

            // EMA component: standard exponential smoothing
            // ema_component = alpha * (input - rema) + rema = rema + alpha * (input - rema)
            double emaComponent = Math.FusedMultiplyAdd(alpha, input - state.Rema, state.Rema);

            // Regularization component: momentum continuation
            // reg_component = rema + (rema - prev_rema)
            double regComponent = state.Rema + (state.Rema - state.PrevRema);

            // REMA = lambda * (ema_component - reg_component) + reg_component
            // When lambda=1: REMA = ema_component (standard EMA)
            // When lambda=0: REMA = reg_component (pure momentum)
            state.Rema = Math.FusedMultiplyAdd(lambda, emaComponent - regComponent, regComponent);
            state.PrevRema = prevRema;

            if (!state.IsCompensated)
            {
                state.E *= decay;

                if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
                {
                    state.IsHot = true;
                }

                if (state.E <= COMPENSATOR_THRESHOLD)
                {
                    state.IsCompensated = true;
                    result = state.Rema;
                }
                else
                {
                    // Apply bias compensation similar to EMA
                    result = state.Rema / (1.0 - state.E);
                }
            }
            else
            {
                result = state.Rema;
            }
        }

        return result;
    }

    /// <summary>
    /// Core REMA calculation for batch processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output, double alpha, double lambda, ref State state, ref double lastValidValue)
    {
        int len = source.Length;
        double decay = 1.0 - alpha;

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

            double result;

            if (!state.IsInitialized)
            {
                state.Rema = val;
                state.PrevRema = val;
                state.IsInitialized = true;
                state.E *= decay;

                if (state.E <= COVERAGE_THRESHOLD)
                {
                    state.IsHot = true;
                }

                result = val;
            }
            else
            {
                double prevRema = state.Rema;

                double emaComponent = Math.FusedMultiplyAdd(alpha, val - state.Rema, state.Rema);
                double regComponent = state.Rema + (state.Rema - state.PrevRema);
                state.Rema = Math.FusedMultiplyAdd(lambda, emaComponent - regComponent, regComponent);
                state.PrevRema = prevRema;

                if (!state.IsCompensated)
                {
                    state.E *= decay;

                    if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
                    {
                        state.IsHot = true;
                    }

                    if (state.E <= COMPENSATOR_THRESHOLD)
                    {
                        state.IsCompensated = true;
                        result = state.Rema;
                    }
                    else
                    {
                        result = state.Rema / (1.0 - state.E);
                    }
                }
                else
                {
                    result = state.Rema;
                }
            }

            Unsafe.Add(ref outRef, i) = result;

        }
    }

    /// <summary>
    /// Runs a high-performance batch calculation and returns a hot REMA instance.
    /// </summary>

    /// <summary>
    /// Calculates REMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double lambda = 0.5)
    {
        var rema = new Rema(period, lambda);
        return rema.Update(source);
    }

    /// <summary>
    /// Calculates REMA in-place using period, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double lambda = 0.5)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (lambda < 0.0 || lambda > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), "Lambda must be between 0 and 1");
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1);

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

        CalculateCore(source, output, alpha, lambda, ref state, ref lastValid);
    }
    public static (TSeries Results, Rema Indicator) Calculate(TSeries source, int period, double lambda = 0.5)
    {
        var rema = new Rema(period, lambda);
        TSeries results = rema.Update(source);
        return (results, rema);
    }
    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}