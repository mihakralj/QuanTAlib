using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// USF: Ehlers Ultimate Smoother Filter
/// </summary>
/// <remarks>
/// USF is a zero-lag smoothing filter introduced by John Ehlers in April 2024.
/// It achieves superior smoothing by subtracting high-frequency components using a high-pass filter.
///
/// Formula:
/// arg = sqrt(2) * PI / period
/// c2 = 2 * exp(-arg) * cos(arg)
/// c3 = -exp(-2 * arg)
/// c1 = (1 + c2 - c3) / 4
/// USF = (1 - c1) * src + (2 * c1 - c2) * src[1] - (c1 + c3) * src[2] + c2 * USF[1] + c3 * USF[2]
///
/// Computation: 5 multiplications, 4 additions per cycle
/// </remarks>
[SkipLocalsInit]
public sealed class Usf : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Usf1, double Usf2, double PrevInput1, double PrevInput2, double LastValidValue, int Count, bool IsHot)
    {
        public static State New() => new() { Usf1 = 0, Usf2 = 0, PrevInput1 = 0, PrevInput2 = 0, LastValidValue = double.NaN, Count = 0, IsHot = false };
    }

    private readonly double _c1, _c2, _c3;
    private readonly double _k0, _k1, _k2; // Precomputed coefficients for FMA
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private State _state = State.New();
    private State _p_state = State.New();

    /// <summary>
    /// Creates USF with specified period.
    /// </summary>
    /// <param name="period">Period for USF calculation (must be > 0)</param>
    public Usf(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double sqrt2_pi = Math.Sqrt(2) * Math.PI;
        double arg = sqrt2_pi / period;
        double exp_arg = Math.Exp(-arg);

        _c2 = 2.0 * exp_arg * Math.Cos(arg);
        _c3 = -exp_arg * exp_arg;
        _c1 = (1.0 + _c2 - _c3) / 4.0;

        // Precompute coefficients for FMA optimization
        _k0 = 1.0 - _c1;           // coefficient for val
        _k1 = 2.0 * _c1 - _c2;     // coefficient for PrevInput1
        _k2 = -(_c1 + _c3);        // coefficient for PrevInput2

        Name = $"Usf({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>
    /// Creates USF with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for USF calculation</param>
    public Usf(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    public Usf(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _publisher = source;
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    public override bool IsHot => _state.IsHot;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        Reset();

        int len = source.Length;
        int i = 0;

        // Find first valid value
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                _state.LastValidValue = source[k];
                _state.Usf1 = _state.LastValidValue;
                _state.Usf2 = _state.LastValidValue;
                _state.PrevInput1 = _state.LastValidValue;
                _state.PrevInput2 = _state.LastValidValue;
                _state.Count = 1;
                i = k + 1;
                break;
            }
        }

        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                _state.LastValidValue = val;
            else
                val = _state.LastValidValue;

            double usf = (_state.Count < 4)
                ? val
                : (1.0 - _c1) * val + (2.0 * _c1 - _c2) * _state.PrevInput1 - (_c1 + _c3) * _state.PrevInput2 + _c2 * _state.Usf1 + _c3 * _state.Usf2;

            _state.Usf2 = _state.Usf1;
            _state.Usf1 = usf;
            _state.PrevInput2 = _state.PrevInput1;
            _state.PrevInput1 = val;
            _state.Count++;
        }

        if (_state.Count >= WarmupPeriod)
            _state.IsHot = true;

        Last = new TValue(DateTime.MinValue, _state.Usf1);

        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
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

        bool initialized = false;
        if (_state.Count == 0)
        {
            _state.Usf1 = val;
            _state.Usf2 = val;
            _state.PrevInput1 = val;
            _state.PrevInput2 = val;
            _state.Count = 1;
            initialized = true;
        }

        double usf = (_state.Count < 4)
            ? val
            : Math.FusedMultiplyAdd(_c3, _state.Usf2,
                Math.FusedMultiplyAdd(_c2, _state.Usf1,
                    Math.FusedMultiplyAdd(_k2, _state.PrevInput2,
                        Math.FusedMultiplyAdd(_k1, _state.PrevInput1, _k0 * val))));

        _state.Usf2 = _state.Usf1;
        _state.Usf1 = usf;
        _state.PrevInput2 = _state.PrevInput1;
        _state.PrevInput1 = val;

        if (isNew && !initialized) _state.Count++;
        if (!_state.IsHot && _state.Count >= WarmupPeriod)
            _state.IsHot = true;

        Last = new TValue(input.Time, usf);
        PubEvent(Last, isNew);
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

        CalculateCore(sourceValues, vSpan, _c1, _c2, _c3, WarmupPeriod, ref state);

        _state = state;

        sourceTimes.CopyTo(tSpan);

        _p_state = _state;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output, double c1, double c2, double c3, int warmupPeriod, ref State state)
    {
        int len = source.Length;
        int i = 0;

        // If starting from scratch (count == 0), find first valid value
        if (state.Count == 0)
        {
            for (; i < len; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    state.LastValidValue = source[i];
                    state.Usf1 = state.LastValidValue;
                    state.Usf2 = state.LastValidValue;
                    state.PrevInput1 = state.LastValidValue;
                    state.PrevInput2 = state.LastValidValue;
                    output[i] = state.LastValidValue;
                    state.Count = 1;
                    i++;
                    break;
                }
                output[i] = double.NaN;
            }
        }

        // Precompute coefficients for FMA (outside loop)
        double k0 = 1.0 - c1;
        double k1 = 2.0 * c1 - c2;
        double k2 = -(c1 + c3);

        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                state.LastValidValue = val;
            else
                val = state.LastValidValue;

            double usf = (state.Count < 4)
                ? val
                : Math.FusedMultiplyAdd(c3, state.Usf2,
                    Math.FusedMultiplyAdd(c2, state.Usf1,
                        Math.FusedMultiplyAdd(k2, state.PrevInput2,
                            Math.FusedMultiplyAdd(k1, state.PrevInput1, k0 * val))));

            state.Usf2 = state.Usf1;
            state.Usf1 = usf;
            state.PrevInput2 = state.PrevInput1;
            state.PrevInput1 = val;
            output[i] = usf;
            state.Count++;
        }

        if (!state.IsHot && state.Count >= warmupPeriod)
            state.IsHot = true;
    }

    public static (TSeries Results, Usf Indicator) Calculate(TSeries source, int period)
    {
        var usf = new Usf(period);
        TSeries results = usf.Update(source);
        return (results, usf);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double sqrt2_pi = Math.Sqrt(2) * Math.PI;
        double arg = sqrt2_pi / period;
        double exp_arg = Math.Exp(-arg);

        double c2 = 2.0 * exp_arg * Math.Cos(arg);
        double c3 = -exp_arg * exp_arg;
        double c1 = (1.0 + c2 - c3) / 4.0;

        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        if (source.Length == 0) return;

        var state = State.New();

        CalculateCore(source, output, c1, c2, c3, period, ref state);
    }

    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        Last = default;
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
