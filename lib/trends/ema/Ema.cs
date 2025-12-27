using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EMA: Exponential Moving Average
/// </summary>
/// <remarks>
/// EMA applies exponential weighting to data points, giving more weight to recent values.
/// Uses a single state variable for O(1) complexity per update.
///
/// Calculation:
/// alpha = 2 / (period + 1)
/// EMA_new = EMA_old + alpha * (newest - EMA_old)
///
/// Initialization:
/// Uses a compensator factor to correct early-stage bias (when n < period).
/// Output = EMA_state / (1 - (1-alpha)^n)
///
/// O(1) update:
/// No buffer required, only previous EMA value and compensator state.
///
/// IsHot:
/// Becomes true when n = ln(0.05) / ln(1 - alpha) 
/// </remarks>
[SkipLocalsInit]
public sealed class Ema : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Ema, double E, bool IsHot, bool IsCompensated)
    {
        public static State New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };
    }

    private readonly double _alpha;
    private readonly double _decay;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;

    /// <summary>
    /// Creates EMA with specified period.
    /// Alpha = 2 / (period + 1)
    /// </summary>
    /// <param name="period">Period for EMA calculation (must be > 0)</param>
    public Ema(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        Name = $"Ema({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates EMA with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for EMA calculation</param>
    public Ema(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    public Ema(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates EMA with specified alpha smoothing factor.
    /// </summary>
    /// <param name="alpha">Smoothing factor (0 < alpha <= 1)</param>
    public Ema(double alpha)
    {
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be greater than 0 and at most 1", nameof(alpha));

        _alpha = alpha;
        _decay = 1.0 - alpha;
        Name = $"Ema(α={alpha:F4})";
        // Approximate period from alpha: alpha = 2/(N+1) => N = 2/alpha - 1
        WarmupPeriod = (int)(2.0 / alpha - 1.0);
    }

    /// <summary>
    /// True if the EMA has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _state.IsHot;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    public override void Prime(ReadOnlySpan<double> source)
    {
        if (source.Length == 0) return;

        // Reset state
        _state = State.New();
        _p_state = State.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        // Run the calculation on the history to update state
        // We don't need the output, just the final state
        int len = source.Length;
        double decay = _decay;
        int i = 0;

        // Find first valid value to seed lastValid
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

        if (!_state.IsCompensated)
        {
            for (; i < len && _state.E > COMPENSATOR_THRESHOLD; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                    _lastValidValue = val;
                else
                    val = _lastValidValue;

                _state.Ema += _alpha * (val - _state.Ema);
                _state.E *= decay;

                if (!_state.IsHot && _state.E <= COVERAGE_THRESHOLD)
                    _state.IsHot = true;
            }
            if (_state.E <= COMPENSATOR_THRESHOLD)
                _state.IsCompensated = true;
        }

        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                _lastValidValue = val;
            else
                val = _lastValidValue;

            _state.Ema += _alpha * (val - _state.Ema);
        }

        // Calculate the initial "Last" value
        double result = _state.IsCompensated ? _state.Ema : _state.Ema / (1.0 - _state.E);

        // Note: We can't infer accurate Time from a simple Span<double>,
        // so we leave 'Last' with default time or user updates it on next Tick.
        Last = new TValue(DateTime.MinValue, result);

        // Backup state for the next update cycle
        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, TValueEventArgs e) => Update(e.Value, e.IsNew);

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

    private const double COVERAGE_THRESHOLD = 0.05;
    private const double COMPENSATOR_THRESHOLD = 1e-10;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        val = Compute(val, _alpha, _decay, ref _state);
        Last = new TValue(input.Time, val);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

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

        CalculateCore(sourceValues, vSpan, _alpha, ref state, ref lastValidValue);

        _state = state;
        _lastValidValue = lastValidValue;

        sourceTimes.CopyTo(tSpan);

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Compute(double input, double alpha, double decay, ref State state)
    {
        // state.Ema += alpha * (input - state.Ema)
        // state.Ema = state.Ema + alpha * input - alpha * state.Ema
        // state.Ema = state.Ema * (1 - alpha) + alpha * input
        // state.Ema = state.Ema * decay + alpha * input
        state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * input);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
                state.IsHot = true;

            if (state.E <= COMPENSATOR_THRESHOLD)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output, double alpha, ref State state, ref double lastValidValue)
    {
        int len = source.Length;
        double decay = 1.0 - alpha;
        int i = 0;

        if (!state.IsCompensated)
        {
            for (; i < len && state.E > COMPENSATOR_THRESHOLD; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                    lastValidValue = val;
                else
                    val = lastValidValue;

                 
                state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * val);
                state.E *= decay;

                if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
                    state.IsHot = true;

                output[i] = state.Ema / (1.0 - state.E);
            }
            if (state.E <= COMPENSATOR_THRESHOLD)
                state.IsCompensated = true;
        }

        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValidValue = val;
            else
                val = lastValidValue;

            // state.Ema += alpha * (val - state.Ema);  // skipcq: S125
            state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * val);
            output[i] = state.Ema;
        }
    }

    /// <summary>
    /// Runs a high-performance batch calculation on history and returns
    /// a "Hot" Ema instance ready to process the next tick immediately.
    /// </summary>
    /// <param name="source">Historical time series</param>
    /// <param name="period">EMA Period</param>
    /// <returns>A tuple containing the full calculation results and the hot indicator instance</returns>
    public static (TSeries Results, Ema Indicator) Calculate(TSeries source, int period)
    {
        var ema = new Ema(period);
        TSeries results = ema.Update(source);
        return (results, ema);
    }

    /// <summary>
    /// Calculates EMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">EMA period</param>
    /// <returns>EMA series</returns>
    public static TSeries Batch(TSeries source, int period)
    {
        var ema = new Ema(period);
        return ema.Update(source);
    }

    /// <summary>
    /// Calculates EMA in-place using period, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// Alpha = 2 / (period + 1)
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">EMA period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double alpha = 2.0 / (period + 1);
        Batch(source, output, alpha);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(source));
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be > 0 and <= 1");

        if (source.Length == 0) return;

        var state = State.New();
        double lastValid = 0;
        bool foundValid = false;

        // Find first valid value to seed lastValid
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

        CalculateCore(source, output, alpha, ref state, ref lastValid);
    }

    /// <summary>
    /// Resets the EMA state.
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
