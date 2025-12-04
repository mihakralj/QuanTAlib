using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EMA: Exponential Moving Average
/// </summary>
/// <remarks>
/// EMA needs very short history buffer and calculates the EMA value using just the
/// previous EMA value. The weight of the new datapoint (alpha) is alpha = 2 / (period + 1)
///
/// Key characteristics:
/// - Uses no buffer, relying only on the previous EMA value.
/// - The weight of new data points is calculated as alpha = 2 / (period + 1).
/// - Provides a balance between responsiveness and smoothing. No overshooting. Significant lag
///
/// Calculation method:
/// This implementation can use SMA for the first Period bars as a seeding value for EMA when useSma is true.
///
/// Sources:
/// - https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
/// - https://www.investopedia.com/ask/answers/122314/what-exponential-moving-average-ema-formula-and-how-ema-calculated.asp
/// - https://blog.fugue88.ws/archives/2017-01/The-correct-way-to-start-an-Exponential-Moving-Average-EMA
/// </remarks>
public class Ema
{
    private struct State : IEquatable<State>
    {
        public double Ema;
        public double E;          // Compensator: decays from 1.0 to 1e-10 for bias correction
        public bool IsHot;          // True when 95% coverage reached (E <= 0.05)
        public bool IsCompensated;  // True when compensator fully decayed (E <= 1e-10)

        public static State New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };

        public readonly bool Equals(State other) =>
            Ema == other.Ema && E == other.E && IsHot == other.IsHot && IsCompensated == other.IsCompensated;

        public override readonly bool Equals(object? obj) =>
            obj is State other && Equals(other);

        public override readonly int GetHashCode() =>
            HashCode.Combine(Ema, E, IsHot, IsCompensated);

        public static bool operator ==(State left, State right) => left.Equals(right);
        public static bool operator !=(State left, State right) => !left.Equals(right);
    }

    private readonly double _alpha;
    private readonly double _decay;  // Pre-calculated (1.0 - alpha) to avoid subtraction per tick
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

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
    public TValue Value { get; private set; }

    /// <summary>
    /// True if the EMA has warmed up and is providing valid results.
    /// </summary>
    public bool IsHot => _state.IsHot;

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
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

    // 95% coverage threshold: E = 1 - 0.95 = 0.05
    private const double COVERAGE_THRESHOLD = 0.05;
    // Compensator decay threshold for bias correction
    private const double COMPENSATOR_THRESHOLD = 1e-10;

    /// <summary>
    /// Core EMA calculation kernel.
    /// Assumes input has already been validated via GetValidValue().
    /// IsHot becomes true at 95% coverage (E <= 0.05).
    /// Bias correction continues until compensator decays to 1e-10.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Compute(double input, double alpha, double decay, ref State state)
    {
        state.Ema += alpha * (input - state.Ema);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            // IsHot triggers at 95% coverage
            if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
                state.IsHot = true;

            // Continue bias correction until compensator fully decays
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

    /// <summary>
    /// Core calculation kernel that handles both batch and streaming-continuation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output, double alpha, ref State state, ref double lastValidValue)
    {
        int len = source.Length;
        double decay = 1.0 - alpha;
        int i = 0;

        // Phase 1: Warmup with bias correction
        // If state is already compensated, this loop is skipped
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

        // Phase 2: Hot loop
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
    /// Updates EMA with the given value.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Compensated EMA value</returns>
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

        // Last-value substitution: replace non-finite inputs with last valid value
        double val = GetValidValue(input.Value);
        val = Compute(val, _alpha, _decay, ref _state);
        Value = new TValue(input.Time, val);
        return Value;
    }

    /// <summary>
    /// Updates EMA with the entire series.
    /// Uses split-loop optimization: warmup phase with bias correction, then branchless hot loop.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>EMA series</returns>
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

        // 1. Fast Batch Calculation
        // Uses the unified CalculateCore to handle both new and continuing states
        // Optimization: Copy state to locals to allow JIT register allocation
        State state = _state;
        double lastValidValue = _lastValidValue;

        CalculateCore(sourceValues, vSpan, _alpha, ref state, ref lastValidValue);

        _state = state;
        _lastValidValue = lastValidValue;

        // Copy Times
        sourceTimes.CopyTo(tSpan);
        
        _p_state = _state;
        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
        
        return new TSeries(t, v);
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

    /// <summary>
    /// Calculates EMA in-place using alpha, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// Bias correction continues until compensator decays to 1e-10.
    /// Uses split-loop optimization: warmup phase with bias correction, then branchless hot loop.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="alpha">Smoothing factor (0 < alpha <= 1)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        if (source.Length == 0) return;

        // Initialize default state for static calculation
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
        Value = default;
    }
}
