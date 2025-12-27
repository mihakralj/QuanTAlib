using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SSF: Ehlers Super Smooth Filter
/// </summary>
/// <remarks>
/// SSF is a 2-pole Butterworth filter that offers superior noise reduction with minimal lag.
///
/// Formula:
/// arg = 1.414 * 3.14159 / period
/// c2 = 2 * exp(-arg) * cos(arg)
/// c3 = -exp(-2 * arg)
/// c1 = 1 - c2 - c3
/// SSF = c1 * (src + src[1]) / 2 + c2 * SSF[1] + c3 * SSF[2]
/// </remarks>
[SkipLocalsInit]
public sealed class Ssf : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Ssf1, double Ssf2, double PrevInput, double LastValidValue, int Count, bool IsHot)
    {
        public static State New() => new() { Ssf1 = 0, Ssf2 = 0, PrevInput = 0, LastValidValue = 0, Count = 0, IsHot = false };
    }

    private readonly double _c1, _c2, _c3;
    private readonly TValuePublishedHandler _handler;
    private State _state = State.New();
    private State _p_state = State.New();

    /// <summary>
    /// Creates SSF with specified period.
    /// </summary>
    /// <param name="period">Period for SSF calculation (must be > 0)</param>
    public Ssf(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        // Use high precision constants
        // Note: Some implementations (like Ooples/PineScript) use 1.414 * 3.14159 which causes divergence
        double sqrt2_pi = Math.Sqrt(2) * Math.PI;
        double arg = sqrt2_pi / period;
        double exp_arg = Math.Exp(-arg);

        // arg is in radians for Math.Cos (EasyLanguage Cosine takes degrees, but 1.414*180/Period is radians in degrees)
        // 1.414 * 180 / Period (degrees) = 1.414 * PI / Period (radians)
        // So arg calculated above is correct for Math.Cos (which takes radians)
        _c2 = 2.0 * exp_arg * Math.Cos(arg);
        _c3 = -exp_arg * exp_arg;
        _c1 = 1.0 - _c2 - _c3;

        Name = $"Ssf({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>
    /// Creates SSF with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for SSF calculation</param>
    public Ssf(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    public Ssf(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += _handler;
    }

    private void Handle(object? sender, TValueEventArgs e) => Update(e.Value, e.IsNew);

    public override bool IsHot => _state.IsHot;

    public override void Prime(ReadOnlySpan<double> source)
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
                _state.Ssf1 = _state.LastValidValue;
                _state.Ssf2 = _state.LastValidValue;
                _state.PrevInput = _state.LastValidValue;
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

            double ssf = (_state.Count < 4)
                ? val
                : (_c1 * (val + _state.PrevInput) * 0.5) + (_c2 * _state.Ssf1) + (_c3 * _state.Ssf2);

            _state.Ssf2 = _state.Ssf1;
            _state.Ssf1 = ssf;
            _state.PrevInput = val;
            _state.Count++;
        }

        if (_state.Count >= WarmupPeriod)
            _state.IsHot = true;

        Last = new TValue(DateTime.MinValue, _state.Ssf1);

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

        if (_state.Count == 0)
        {
            _state.Ssf1 = val;
            _state.Ssf2 = val;
            _state.PrevInput = val;
        }

        double ssf = (_state.Count < 4)
            ? val
            : (_c1 * (val + _state.PrevInput) * 0.5) + (_c2 * _state.Ssf1) + (_c3 * _state.Ssf2);
        
        _state.Ssf2 = _state.Ssf1;
        _state.Ssf1 = ssf;
        _state.PrevInput = val;
        
        if (isNew) _state.Count++;
        if (!_state.IsHot && _state.Count >= WarmupPeriod)
            _state.IsHot = true;

        Last = new TValue(input.Time, ssf);
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
                    state.Ssf1 = state.LastValidValue;
                    state.Ssf2 = state.LastValidValue;
                    state.PrevInput = state.LastValidValue;
                    output[i] = state.LastValidValue; 
                    state.Count = 1;
                    i++;
                    break;
                }
                output[i] = double.NaN; 
            }
        }

        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                state.LastValidValue = val;
            else
                val = state.LastValidValue;

            double ssf = (state.Count < 4)
                ? val
                : (c1 * (val + state.PrevInput) * 0.5) + (c2 * state.Ssf1) + (c3 * state.Ssf2);

            state.Ssf2 = state.Ssf1;
            state.Ssf1 = ssf;
            state.PrevInput = val;
            output[i] = ssf;
            state.Count++;
        }

        if (!state.IsHot && state.Count >= warmupPeriod)
            state.IsHot = true;
    }

    public static (TSeries Results, Ssf Indicator) Calculate(TSeries source, int period)
    {
        var ssf = new Ssf(period);
        TSeries results = ssf.Update(source);
        return (results, ssf);
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
        double c1 = 1.0 - c2 - c3;

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
}
