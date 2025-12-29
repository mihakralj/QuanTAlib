using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// T3: Tillson T3 Moving Average
/// </summary>
/// <remarks>
/// T3 works by running price data through a series of six EMAs, then combining the outputs 
/// of these EMAs using carefully calculated weights.
/// 
/// Formula:
/// T3 = c1*e6 + c2*e5 + c3*e4 + c4*e3
/// 
/// Where:
/// e1..e6 are cascaded EMAs
/// c1 = -v^3
/// c2 = 3(v^2 + v^3)
/// c3 = -3(2v^2 + v + v^3)
/// c4 = 1 + 3v + 3v^2 + v^3
/// 
/// v is volume factor (default 0.7)
/// alpha = 2 / (period + 1)
/// </remarks>
[SkipLocalsInit]
public sealed class T3 : AbstractBase, IDisposable
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double E1, double E2, double E3, double E4, double E5, double E6, bool IsInitialized)
    {
        public static State New() => new() { IsInitialized = false };
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Parameters(double Alpha, double C1, double C2, double C3, double C4);

    private readonly Parameters _params;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;

    /// <summary>
    /// Creates T3 with specified period and volume factor.
    /// </summary>
    /// <param name="period">Period for EMA calculation (must be > 0)</param>
    /// <param name="vfactor">Volume Factor (default 0.7)</param>
    public T3(int period, double vfactor = 0.7)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (!double.IsFinite(vfactor))
            throw new ArgumentOutOfRangeException(nameof(vfactor), "Volume factor must be a finite number (not NaN or Infinity)");
        if (vfactor <= 0 || vfactor > 1)
            throw new ArgumentOutOfRangeException(nameof(vfactor), "Volume factor must be greater than 0 and typically <= 1");

        double alpha = 2.0 / (period + 1);

        // Precompute coefficients
        double v = vfactor;
        double v2 = v * v;
        double v3 = v2 * v;

        double c1 = -v3;
        double c2 = 3.0 * (v2 + v3);
        double c3 = -3.0 * (2.0 * v2 + v + v3);
        double c4 = 1.0 + 3.0 * v + 3.0 * v2 + v3;

        _params = new Parameters(alpha, c1, c2, c3, c4);

        Name = $"T3({period}, {vfactor:F2})";
        WarmupPeriod = period * 6; // T3 has 6 cascaded EMAs, so warmup is longer
    }

    /// <summary>
    /// Creates T3 with specified source, period and volume factor.
    /// Subscribes to source.Pub event.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for EMA calculation</param>
    /// <param name="vfactor">Volume Factor (default 0.7)</param>
    public T3(ITValuePublisher source, int period, double vfactor = 0.7) : this(period, vfactor)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates T3 with specified source, period and volume factor.
    /// </summary>
    /// <param name="source">Source series</param>
    /// <param name="period">Period for EMA calculation</param>
    /// <param name="vfactor">Volume Factor (default 0.7)</param>
    public T3(TSeries source, int period, double vfactor = 0.7) : this(period, vfactor)
    {
        _publisher = source;
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _handler = Handle;
        _publisher.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the T3 has been initialized (received at least one value).
    /// </summary>
    public override bool IsHot => _state.IsInitialized;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
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
        double lastValidValue = 0;
        State state = _state;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValidValue = val;
            else
                val = lastValidValue;

            Compute(val, _params, ref state);
        }

        _state = state;
        _lastValidValue = lastValidValue;

        // Calculate the initial "Last" value
        // We need to re-compute the last step to get the result, or just use the state if we stored the result
        // Since Compute returns the result but also updates state, we can't easily get the last result without re-running or storing it.
        // However, Prime is usually followed by Update or we just need the state ready.
        // If we want Last to be correct, we should probably store the last result.
        // But AbstractBase.Prime doesn't strictly require Last to be set to the very last value of source, 
        // though it's good practice.
        // Let's re-run the last value computation to set Last correctly.
        if (len > 0)
        {
            // We need to be careful not to double-apply the last update if we just loop.
            // Actually, the loop above updated the state to include the last value.
            // So the state corresponds to "after processing source".
            // To get the output value corresponding to the last input, we can calculate it from the state.
            // But T3 formula uses the *updated* EMAs.
            // T3 = c1*e6 + c2*e5 + c3*e4 + c4*e3
            // The state has the updated EMAs.
            double result = _params.C1 * _state.E6 + _params.C2 * _state.E5 + _params.C3 * _state.E4 + _params.C4 * _state.E3;
            Last = new TValue(DateTime.MinValue, result);
        }

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
    }

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
        val = Compute(val, _params, ref _state);
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

        CalculateCore(sourceValues, vSpan, _params, ref state, ref lastValidValue);

        _state = state;
        _lastValidValue = lastValidValue;

        sourceTimes.CopyTo(tSpan);

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Compute(double input, in Parameters p, ref State state)
    {
        if (!state.IsInitialized)
        {
            state.E1 = state.E2 = state.E3 = state.E4 = state.E5 = state.E6 = input;
            state.IsInitialized = true;
        }
        else
        {
            state.E1 += p.Alpha * (input - state.E1);
            state.E2 += p.Alpha * (state.E1 - state.E2);
            state.E3 += p.Alpha * (state.E2 - state.E3);
            state.E4 += p.Alpha * (state.E3 - state.E4);
            state.E5 += p.Alpha * (state.E4 - state.E5);
            state.E6 += p.Alpha * (state.E5 - state.E6);
        }

        return p.C1 * state.E6 + p.C2 * state.E5 + p.C3 * state.E4 + p.C4 * state.E3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output, in Parameters p, ref State state, ref double lastValidValue)
    {
        int len = source.Length;
        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValidValue = val;
            else
                val = lastValidValue;

            output[i] = Compute(val, p, ref state);
        }
    }

    /// <summary>
    /// Calculates T3 for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double vfactor = 0.7)
    {
        var t3 = new T3(period, vfactor);
        return t3.Update(source);
    }

    /// <summary>
    /// Calculates T3 in-place using period, writing results to pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double vfactor = 0.7)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (!double.IsFinite(vfactor))
            throw new ArgumentOutOfRangeException(nameof(vfactor), "Volume factor must be a finite number (not NaN or Infinity)");
        if (vfactor <= 0 || vfactor > 1)
            throw new ArgumentOutOfRangeException(nameof(vfactor), "Volume factor must be greater than 0 and typically <= 1");

        double alpha = 2.0 / (period + 1);
        double v = vfactor;
        double v2 = v * v;
        double v3 = v2 * v;

        double c1 = -v3;
        double c2 = 3.0 * (v2 + v3);
        double c3 = -3.0 * (2.0 * v2 + v + v3);
        double c4 = 1.0 + 3.0 * v + 3.0 * v2 + v3;

        var p = new Parameters(alpha, c1, c2, c3, c4);
        var state = State.New();
        double lastValidValue = 0;

        CalculateCore(source, output, p, ref state, ref lastValidValue);
    }

    /// <summary>
    /// Resets the T3 state.
    /// </summary>
    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }

    public void Dispose()
    {
        if (_publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
            _handler = null;
        }
    }
}
