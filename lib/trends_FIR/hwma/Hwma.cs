// Hwma.cs - Holt-Winters Moving Average
// Triple exponential smoothing with level, velocity, and acceleration components.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HWMA: Holt-Winters Moving Average
/// A triple exponential smoothing filter that tracks level (F), velocity (V), and
/// acceleration (A) components for adaptive trend following.
/// </summary>
/// <remarks>
/// <b>Key characteristics</b>
/// <list type="bullet">
///   <item><description>Triple exponential smoothing with level, velocity, and acceleration</description></item>
///   <item><description>Adapts quickly to trend changes via higher-order derivatives</description></item>
///   <item><description>When period specified: α = 2/(period+1), β = γ = 1/period</description></item>
///   <item><description>O(1) complexity per bar - no windowing required</description></item>
/// </list>
///
/// <b>Calculation</b>
/// <code>
/// F = α × source + (1-α) × (prevF + prevV + 0.5 × prevA)
/// V = β × (F - prevF) + (1-β) × (prevV + prevA)
/// A = γ × (V - prevV) + (1-γ) × prevA
/// output = F + V + 0.5 × A
/// </code>
///
/// <b>Sources</b>
///   Holt, C.E. (1957) - "Forecasting Seasonals and Trends by Exponentially Weighted Moving Averages"
///   Winters, P.R. (1960) - "Forecasting Sales by Exponentially Weighted Moving Averages"
/// </remarks>
[SkipLocalsInit]
public sealed class Hwma : AbstractBase
{
    private readonly double _alpha;
    private readonly double _beta;
    private readonly double _gamma;
    private readonly double _decayAlpha;
    private readonly double _decayBeta;
    private readonly double _decayGamma;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _pubHandler;
    private bool _isNew = true;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double F, double V, double A,
        double LastValidValue,
        bool IsInitialized
    );
    private State _state;
    private State _p_state;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.IsInitialized;

    /// <summary>
    /// Creates HWMA with specified period. Calculates α, β, γ automatically.
    /// </summary>
    /// <param name="period">Period for smoothing factor calculation (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hwma(int period = 10)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _alpha = 2.0 / (period + 1.0);
        _beta = 1.0 / period;
        _gamma = 1.0 / period;
        _decayAlpha = 1.0 - _alpha;
        _decayBeta = 1.0 - _beta;
        _decayGamma = 1.0 - _gamma;
        Name = $"Hwma({period})";
        WarmupPeriod = period;

        _state = new State(double.NaN, 0, 0, double.NaN, IsInitialized: false);
    }

    /// <summary>
    /// Creates HWMA with explicit smoothing factors.
    /// </summary>
    /// <param name="alpha">Level smoothing factor (0 to 1)</param>
    /// <param name="beta">Velocity smoothing factor (0 to 1)</param>
    /// <param name="gamma">Acceleration smoothing factor (0 to 1)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hwma(double alpha, double beta, double gamma)
    {
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 (exclusive) and 1 (inclusive)", nameof(alpha));
        if (beta < 0 || beta > 1)
            throw new ArgumentException("Beta must be between 0 and 1", nameof(beta));
        if (gamma < 0 || gamma > 1)
            throw new ArgumentException("Gamma must be between 0 and 1", nameof(gamma));

        int effectivePeriod = (int)(2.0 / alpha - 1.0); // Reverse calculate for display
        _alpha = alpha;
        _beta = beta;
        _gamma = gamma;
        _decayAlpha = 1.0 - alpha;
        _decayBeta = 1.0 - beta;
        _decayGamma = 1.0 - gamma;
        Name = $"Hwma({alpha:F3},{beta:F3},{gamma:F3})";
        WarmupPeriod = effectivePeriod > 0 ? effectivePeriod : 10;

        _state = new State(double.NaN, 0, 0, double.NaN, IsInitialized: false);
    }

    /// <param name="source">Data source for event-based updates</param>
    /// <param name="period">Period for smoothing factor calculation (default: 10)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hwma(ITValuePublisher source, int period = 10) : this(period)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    protected override void Dispose(bool disposing)
    {
        if (disposing && _source != null && _pubHandler != null)
        {
            _source.Pub -= _pubHandler;
        }
        base.Dispose(disposing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            return input;
        }
        return _state.IsInitialized ? _state.LastValidValue : double.NaN;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        return Update(input, isNew, publish: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue Update(TValue input, bool isNew, bool publish)
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

        if (!double.IsFinite(val))
        {
            // First value is NaN - return NaN
            Last = new TValue(input.Time, double.NaN);
            if (publish) PubEvent(Last);
            return Last;
        }

        _state = _state with { LastValidValue = val };

        double result;

        if (!_state.IsInitialized)
        {
            // First valid value: initialize F to source, V and A to 0
            _state = _state with { F = val, V = 0, A = 0, IsInitialized = true };
            result = val;
        }
        else
        {
            double prevF = _state.F;
            double prevV = _state.V;
            double prevA = _state.A;

            // F = α × source + (1-α) × (prevF + prevV + 0.5 × prevA)
            double forecast = prevF + prevV + 0.5 * prevA;
            double newF = Math.FusedMultiplyAdd(forecast, _decayAlpha, _alpha * val);

            // V = β × (F - prevF) + (1-β) × (prevV + prevA)
            double newV = Math.FusedMultiplyAdd(prevV + prevA, _decayBeta, _beta * (newF - prevF));

            // A = γ × (V - prevV) + (1-γ) × prevA
            double newA = Math.FusedMultiplyAdd(prevA, _decayGamma, _gamma * (newV - prevV));

            _state = _state with { F = newF, V = newV, A = newA };

            // output = F + V + 0.5 × A
            result = newF + newV + 0.5 * newA;
        }

        Last = new TValue(input.Time, result);
        if (publish)
        {
            PubEvent(Last);
        }
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);

        // HWMA has IIR filter state (F, V, A) that accumulates from the beginning.
        // Must process entire series through streaming to maintain correct state.
        Reset();
        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i], isNew: true, publish: false);
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Calculates HWMA from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 10)
    {
        var hwma = new Hwma(period);
        return hwma.Update(source);
    }

    /// <summary>
    /// Calculates HWMA over a span of values.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output buffer (must be same length as source)</param>
    /// <param name="period">Period for smoothing factors (default: 10)</param>
    /// <exception cref="ArgumentException">Thrown when output length doesn't match source length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 10)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        if (source.Length == 0) return;

        double alpha = 2.0 / (period + 1.0);
        double beta = 1.0 / period;
        double gamma = 1.0 / period;
        double decayAlpha = 1.0 - alpha;
        double decayBeta = 1.0 - beta;
        double decayGamma = 1.0 - gamma;

        double lastValid = double.NaN;
        double F = double.NaN;
        double V = 0;
        double A = 0;
        bool initialized = false;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];

            // Handle NaN - use last valid
            if (!double.IsFinite(val))
            {
                if (double.IsFinite(lastValid))
                {
                    val = lastValid;
                }
                else
                {
                    output[i] = double.NaN; // No valid value yet
                    continue;
                }
            }

            lastValid = val;

            if (!initialized)
            {
                F = val;
                V = 0;
                A = 0;
                initialized = true;
                output[i] = val;
            }
            else
            {
                double prevF = F;
                double prevV = V;
                double prevA = A;

                // F = α × source + (1-α) × (prevF + prevV + 0.5 × prevA)
                F = Math.FusedMultiplyAdd(prevF + prevV + 0.5 * prevA, decayAlpha, alpha * val);

                // V = β × (F - prevF) + (1-β) × (prevV + prevA)
                V = Math.FusedMultiplyAdd(prevV + prevA, decayBeta, beta * (F - prevF));

                // A = γ × (V - prevV) + (1-γ) × prevA
                A = Math.FusedMultiplyAdd(prevA, decayGamma, gamma * (V - prevV));

                // output = F + V + 0.5 × A
                output[i] = F + V + 0.5 * A;
            }
        }
    }

    public override void Reset()
    {
        _state = new State(double.NaN, 0, 0, double.NaN, IsInitialized: false);
        _p_state = _state;
        Last = default;
    }
}