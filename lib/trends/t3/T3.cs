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
public sealed class T3 : ITValuePublisher
{
    private struct State : IEquatable<State>
    {
        public double E1, E2, E3, E4, E5, E6;
        public bool IsInitialized;

        public static State New() => new() { IsInitialized = false };

        public override bool Equals(object? obj) => obj is State other && Equals(other);

#pragma warning disable S1244 // Do not check floating point equality with exact values
        public bool Equals(State other) =>
            E1 == other.E1 && E2 == other.E2 && E3 == other.E3 &&
            E4 == other.E4 && E5 == other.E5 && E6 == other.E6 &&
            IsInitialized == other.IsInitialized;
#pragma warning restore S1244 // Do not check floating point equality with exact values

        public override int GetHashCode() => HashCode.Combine(E1, E2, E3, E4, E5, E6, IsInitialized);

        public static bool operator ==(State left, State right) => left.Equals(right);

        public static bool operator !=(State left, State right) => !left.Equals(right);
    }

    private readonly struct Parameters : IEquatable<Parameters>
    {
        public readonly double Alpha;
        public readonly double C1, C2, C3, C4;

        public Parameters(double alpha, double c1, double c2, double c3, double c4)
        {
            Alpha = alpha;
            C1 = c1;
            C2 = c2;
            C3 = c3;
            C4 = c4;
        }

        public override bool Equals(object? obj) => obj is Parameters other && Equals(other);

#pragma warning disable S1244 // Do not check floating point equality with exact values
        public bool Equals(Parameters other) =>
            Alpha == other.Alpha &&
            C1 == other.C1 && C2 == other.C2 &&
            C3 == other.C3 && C4 == other.C4;
#pragma warning restore S1244 // Do not check floating point equality with exact values

        public override int GetHashCode() => HashCode.Combine(Alpha, C1, C2, C3, C4);

        public static bool operator ==(Parameters left, Parameters right) => left.Equals(right);

        public static bool operator !=(Parameters left, Parameters right) => !left.Equals(right);
    }

    private readonly Parameters _params;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Creates T3 with specified period and volume factor.
    /// </summary>
    /// <param name="period">Period for EMA calculation (must be > 0)</param>
    /// <param name="vfactor">Volume Factor (default 0.7)</param>
    public T3(int period, double vfactor = 0.7)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

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
        source.Pub += (item) => Update(item);
    }

    /// <summary>
    /// Current T3 value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the T3 has been initialized (received at least one value).
    /// </summary>
    public bool IsHot => _state.IsInitialized;

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
        val = Compute(val, _params, ref _state);
        Last = new TValue(input.Time, val);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

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
    public static TSeries Calculate(TSeries source, int period, double vfactor = 0.7)
    {
        var t3 = new T3(period, vfactor);
        return t3.Update(source);
    }

    /// <summary>
    /// Calculates T3 in-place using period, writing results to pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, double vfactor = 0.7)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

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
    public void Reset()
    {
        _state = State.New();
        _p_state = _state;
        _lastValidValue = 0;
        Last = default;
    }
}
