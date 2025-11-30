using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

public struct EmaState
{
    public double Ema { get; set; }
    public double E { get; set; }
    public bool IsHot { get; set; }

    public static EmaState New() => new() { Ema = 0, E = 1.0, IsHot = false };
}

/// <summary>
/// Exponential Moving Average (EMA) - IIR filter with exponential warmup compensator.
/// Provides valid output from first bar with O(1) complexity.
/// </summary>
/// <remarks>
/// Algorithm uses exponential smoothing with compensator for immediate valid results.
/// Reference: https://github.com/mihakralj/pinescript/blob/main/indicators/trends_IIR/ema.md
/// </remarks>
public class Ema
{
    private readonly double _alpha;
    private EmaState _state = EmaState.New();
    private EmaState _p_state = EmaState.New();
    private double _lastValidValue;

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
    }

    /// <summary>
    /// Creates EMA with specified alpha smoothing factor.
    /// </summary>
    /// <param name="alpha">Smoothing factor (0 < alpha <= 1)</param>
    public Ema(double alpha)
    {
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        _alpha = alpha;
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
    public static double Compute(double input, double alpha, ref EmaState state)
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
        EmaState state = _state;

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
    /// Resets the EMA state.
    /// </summary>
    public void Reset()
    {
        _state = EmaState.New();
        _p_state = _state;
        _lastValidValue = 0;
        Value = default;
    }
}
