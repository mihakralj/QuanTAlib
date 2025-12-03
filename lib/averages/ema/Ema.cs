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
        public double E;
        public bool IsHot;

        public static State New() => new() { Ema = 0, E = 1.0, IsHot = false };

        public readonly bool Equals(State other) =>
            Ema == other.Ema && E == other.E && IsHot == other.IsHot;

        public override readonly bool Equals(object? obj) =>
            obj is State other && Equals(other);

        public override readonly int GetHashCode() =>
            HashCode.Combine(Ema, E, IsHot);

        public static bool operator ==(State left, State right) => left.Equals(right);
        public static bool operator !=(State left, State right) => !left.Equals(right);
    }

    private readonly double _alpha;
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

    /// <summary>
    /// Core EMA calculation kernel.
    /// Assumes input has already been validated via GetValidValue().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Compute(double input, double alpha, ref State state)
    {
        state.Ema += alpha * (input - state.Ema);

        double result;
        if (!state.IsHot)
        {
            state.E *= (1.0 - alpha);
            state.IsHot = state.E <= 1e-10;
            result = state.Ema / (1.0 - state.E);
        }
        else
        {
            result = state.Ema;
        }

        return result;
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
        val = Compute(val, _alpha, ref _state);
        Value = new TValue(input.Time, val);
        return Value;
    }

    /// <summary>
    /// Updates EMA with the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>EMA series</returns>
    public TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        // Local state for batch processing
        State state = _state;

        for (int i = 0; i < len; i++)
        {
            // Last-value substitution: replace non-finite inputs with last valid value
            double val = GetValidValue(sourceValues[i]);
            val = Compute(val, _alpha, ref state);
            tSpan[i] = sourceTimes[i];
            vSpan[i] = val;
        }

        // Update instance state to the final state
        _state = state;
        _p_state = state; // Assume last point is committed

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
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="alpha">Smoothing factor (0 &lt; alpha &lt;= 1)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        int len = source.Length;
        double ema = 0;
        double e = 1.0;
        double lastValid = 0;
        double oneMinusAlpha = 1.0 - alpha;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
                val = lastValid;
            else
                lastValid = val;

            ema += alpha * (val - ema);
            e *= oneMinusAlpha;

            // Bias correction until warmed up
            output[i] = e > 1e-10 ? ema / (1.0 - e) : ema;
        }
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
