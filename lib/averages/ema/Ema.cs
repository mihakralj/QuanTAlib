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
public sealed class Ema : ITValuePublisher
{
    private struct State
    {
        public double Ema;
        public double E;
        public bool IsHot;
        public bool IsCompensated;

        public static State New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };
    }

    private readonly double _alpha;
    private readonly double _decay;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

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
    }

    /// <summary>
    /// Creates EMA with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for EMA calculation</param>
    public Ema(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    /// <summary>
    /// Creates EMA with specified alpha smoothing factor.
    /// </summary>
    /// <param name="alpha">Smoothing factor (0 &lt; alpha &lt;= 1)</param>
    public Ema(double alpha)
    {
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        _alpha = alpha;
        _decay = 1.0 - alpha;
        Name = $"Ema(α={alpha:F4})";
    }

    /// <summary>
    /// Current EMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the EMA has warmed up and is providing valid results.
    /// </summary>
    public bool IsHot => _state.IsHot;

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
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value);
        val = Compute(val, _alpha, _decay, ref _state);
        Last = new TValue(input.Time, val);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries(new List<long>(), new List<double>());

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
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Compute(double input, double alpha, double decay, ref State state)
    {
        state.Ema += alpha * (input - state.Ema);

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

                state.Ema += alpha * (val - state.Ema);
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

            state.Ema += alpha * (val - state.Ema);
            output[i] = state.Ema;
        }
    }

    /// <summary>
    /// Calculates EMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">EMA period</param>
    /// <returns>EMA series</returns>
    public static TSeries Calculate(TSeries source, int period)
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
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double alpha = 2.0 / (period + 1);
        Calculate(source, output, alpha);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        if (source.Length == 0) return;

        State state = State.New();
        double lastValid = 0;

        CalculateCore(source, output, alpha, ref state, ref lastValid);
    }

    /// <summary>
    /// Resets the EMA state.
    /// </summary>
    public void Reset()
    {
        _state = State.New();
        _p_state = _state;
        _lastValidValue = 0;
        Last = default;
    }
}
