using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DEMA: Double Exponential Moving Average
/// </summary>
/// <remarks>
/// DEMA reduces the lag of traditional EMA by subtracting the lag from the original EMA.
///
/// Calculation:
/// EMA1 = EMA(input)
/// EMA2 = EMA(EMA1)
/// DEMA = 2 * EMA1 - EMA2
///
/// O(1) update:
/// Uses two EMA instances, each with O(1) update complexity.
///
/// IsHot:
/// Becomes true when the second EMA converges (approx. 2x EMA convergence time).
/// </remarks>
[SkipLocalsInit]
public sealed class Dema : ITValuePublisher
{
    private struct EmaState : IEquatable<EmaState>
    {
        public double Ema;
        public double E;
        public bool IsHot;
        public bool IsCompensated;

        public static EmaState New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };

        public override bool Equals(object? obj) => obj is EmaState other && Equals(other);

        public bool Equals(EmaState other) =>
            Ema == other.Ema &&
            E == other.E &&
            IsHot == other.IsHot &&
            IsCompensated == other.IsCompensated;

        public override int GetHashCode() => HashCode.Combine(Ema, E, IsHot, IsCompensated);

        public static bool operator ==(EmaState left, EmaState right) => left.Equals(right);

        public static bool operator !=(EmaState left, EmaState right) => !left.Equals(right);
    }

    private readonly double _alpha;
    private readonly double _decay;
    
    private EmaState _state1 = EmaState.New();
    private EmaState _state2 = EmaState.New();
    private EmaState _p_state1 = EmaState.New();
    private EmaState _p_state2 = EmaState.New();
    
    private double _lastValidValue;
    private double _p_lastValidValue;

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _state2.IsHot;
    public event Action<TValue>? Pub;

    public Dema(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        Name = $"Dema({period})";
    }

    public Dema(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    public Dema(double alpha)
    {
        if (alpha <= 0 || alpha > 1) throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        _alpha = alpha;
        _decay = 1.0 - alpha;
        Name = $"Dema(α={alpha:F4})";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state1 = _state1;
            _p_state2 = _state2;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state1 = _p_state1;
            _state2 = _p_state2;
            _lastValidValue = _p_lastValidValue;
        }

        // EMA1
        double val = input.Value;
        if (double.IsFinite(val))
            _lastValidValue = val;
        else
            val = _lastValidValue;

        double e1 = Compute(val, _alpha, _decay, ref _state1);

        // EMA2 (input is e1, which is always valid)
        double e2 = Compute(e1, _alpha, _decay, ref _state2);

        double result = 2 * e1 - e2;
        Last = new TValue(input.Time, result);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        source.Times.CopyTo(tSpan);

        var sourceValues = source.Values;
        
        // Use current state
        EmaState s1 = _state1;
        EmaState s2 = _state2;
        double lastValid = _lastValidValue;
        double alpha = _alpha;
        double decay = _decay;

        for (int i = 0; i < len; i++)
        {
            double val = sourceValues[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double e1 = Compute(val, alpha, decay, ref s1);
            double e2 = Compute(e1, alpha, decay, ref s2);

            vSpan[i] = 2 * e1 - e2;
        }

        // Update instance state
        _state1 = s1;
        _state2 = s2;
        _p_state1 = s1;
        _p_state2 = s2;
        _lastValidValue = lastValid;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Compute(double input, double alpha, double decay, ref EmaState state)
    {
        state.Ema += alpha * (input - state.Ema);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            if (!state.IsHot && state.E <= 0.05) // COVERAGE_THRESHOLD
                state.IsHot = true;

            if (state.E <= 1e-10) // COMPENSATOR_THRESHOLD
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

    public static TSeries Calculate(TSeries source, int period)
    {
        var dema = new Dema(period);
        return dema.Update(source);
    }

    public static TSeries Calculate(TSeries source, double alpha)
    {
        var dema = new Dema(alpha);
        return dema.Update(source);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double alpha = 2.0 / (period + 1);
        Calculate(source, output, alpha);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        if (source.Length == 0) return;

        double decay = 1.0 - alpha;
        double lastValid = 0;

        // State for EMA1
        double ema1_val = 0;
        double ema1_e = 1.0;
        bool ema1_isCompensated = false;

        // State for EMA2
        double ema2_val = 0;
        double ema2_e = 1.0;
        bool ema2_isCompensated = false;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // Update EMA1
            ema1_val += alpha * (val - ema1_val);
            double e1;
            if (!ema1_isCompensated)
            {
                ema1_e *= decay;
                if (ema1_e <= 1e-10)
                {
                    ema1_isCompensated = true;
                    e1 = ema1_val;
                }
                else
                {
                    e1 = ema1_val / (1.0 - ema1_e);
                }
            }
            else
            {
                e1 = ema1_val;
            }

            // Update EMA2 (input is e1)
            ema2_val += alpha * (e1 - ema2_val);
            double e2;
            if (!ema2_isCompensated)
            {
                ema2_e *= decay;
                if (ema2_e <= 1e-10)
                {
                    ema2_isCompensated = true;
                    e2 = ema2_val;
                }
                else
                {
                    e2 = ema2_val / (1.0 - ema2_e);
                }
            }
            else
            {
                e2 = ema2_val;
            }

            // DEMA = 2 * EMA1 - EMA2
            output[i] = 2 * e1 - e2;
        }
    }

    public void Reset()
    {
        _state1 = EmaState.New();
        _state2 = EmaState.New();
        _p_state1 = EmaState.New();
        _p_state2 = EmaState.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}
