using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BESSEL: 2nd-order Bessel Low-pass Filter
/// </summary>
/// <remarks>
/// Bessel filter is a 2nd-order IIR low-pass filter with maximally flat group delay,
/// adapted from John Ehlers' work for financial time series.
/// 
/// Coefficients for a given length L:
/// a  = exp(-PI / L)
/// b  = 2 * a * cos(1.738 * PI / L)
/// c2 = b
/// c3 = -a * a
/// c1 = 1 - c2 - c3
///
/// Recursive form:
/// F[n] = c1 * Src[n] + c2 * F[n-1] + c3 * F[n-2]
/// </remarks>
[SkipLocalsInit]
public sealed class Bessel : AbstractBase, IDisposable
{
    private record struct State(double F1, double F2, double LastValidValue, int Count, bool IsHot)
    {
        public static State New() => new()
        {
            F1 = 0,
            F2 = 0,
            LastValidValue = 0,
            Count = 0,
            IsHot = false
        };
    }

    private readonly double _c1, _c2, _c3;
    private readonly ITValuePublisher? _publisher;
    private readonly Action<TValue>? _handler;
    private State _state = State.New();
    private State _p_state = State.New();

    /// <summary>
    /// Creates Bessel filter with specified length.
    /// </summary>
    /// <param name="length">Cutoff length (must be > 0, internally clamped to at least 2).</param>
    public Bessel(int length)
    {
        if (length <= 0)
            throw new ArgumentException("Length must be greater than 0", nameof(length));

        int safeLength = Math.Max(length, 2);

        double a = Math.Exp(-Math.PI / safeLength);
        double b = 2.0 * a * Math.Cos(1.738 * Math.PI / safeLength);
        _c2 = b;
        _c3 = -a * a;
        _c1 = 1.0 - _c2 - _c3;

        Name = $"Bessel({length})";
        WarmupPeriod = length;
    }

    /// <summary>
    /// Creates Bessel filter subscribed to a source publisher.
    /// </summary>
    public Bessel(ITValuePublisher source, int length) : this(length)
    {
        _publisher = source;
        _handler = item => Update(item);
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates Bessel filter pre-primed with an existing TSeries and subscribed for future updates.
    /// </summary>
    public Bessel(TSeries source, int length) : this(length)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }

        _publisher = source;
        _handler = item => Update(item);
        source.Pub += _handler;
    }

    public override bool IsHot => _state.IsHot;

    public override void Prime(ReadOnlySpan<double> source)
    {
        if (source.Length == 0)
            return;

        Reset();

        int len = source.Length;
        int i = 0;

        // Find first valid value
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                _state.LastValidValue = source[k];
                _state.F1 = _state.LastValidValue;
                _state.F2 = _state.LastValidValue;
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

            double filt = _state.Count < 3
                ? val
                : (_c1 * val) + (_c2 * _state.F1) + (_c3 * _state.F2);

            _state.F2 = _state.F1;
            _state.F1 = filt;
            _state.Count++;
        }

        if (_state.Count >= WarmupPeriod)
            _state.IsHot = true;

        Last = new TValue(DateTime.MinValue, _state.F1);

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
            _state.F1 = val;
            _state.F2 = val;
        }

        double filt = _state.Count < 3
            ? val
            : (_c1 * val) + (_c2 * _state.F1) + (_c3 * _state.F2);

        _state.F2 = _state.F1;
        _state.F1 = filt;

        if (isNew)
        {
            _state.Count++;
        }

        if (!_state.IsHot && _state.Count >= WarmupPeriod)
            _state.IsHot = true;

        Last = new TValue(input.Time, filt);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
            return [];

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
    private static void CalculateCore(
        ReadOnlySpan<double> source,
        Span<double> output,
        double c1,
        double c2,
        double c3,
        int warmupPeriod,
        ref State state)
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
                    state.F1 = state.LastValidValue;
                    state.F2 = state.LastValidValue;
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

            double filt = state.Count < 3
                ? val
                : (c1 * val) + (c2 * state.F1) + (c3 * state.F2);

            state.F2 = state.F1;
            state.F1 = filt;
            output[i] = filt;
            state.Count++;
        }

        if (!state.IsHot && state.Count >= warmupPeriod)
            state.IsHot = true;
    }

    public static (TSeries Results, Bessel Indicator) Calculate(TSeries source, int length)
    {
        var bessel = new Bessel(length);
        TSeries results = bessel.Update(source);
        return (results, bessel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int length)
    {
        if (length <= 0)
            throw new ArgumentException("Length must be greater than 0", nameof(length));

        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        if (source.Length == 0)
            return;

        int safeLength = Math.Max(length, 2);

        double a = Math.Exp(-Math.PI / safeLength);
        double b = 2.0 * a * Math.Cos(1.738 * Math.PI / safeLength);
        double c2 = b;
        double c3 = -a * a;
        double c1 = 1.0 - c2 - c3;

        var state = State.New();

        CalculateCore(source, output, c1, c2, c3, length, ref state);
    }

    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        Last = default;
    }

    public void Dispose()
    {
        if (_publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
    }
}
