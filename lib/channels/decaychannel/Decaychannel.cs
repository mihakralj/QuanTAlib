using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DECAYCHANNEL: Decay Min-Max Channel
/// Tracks highest high and lowest low with exponential decay toward their midpoint.
/// Uses ln(2)/period for true half-life behavior: 50% convergence over period bars.
/// </summary>
[SkipLocalsInit]
public sealed class Decaychannel : ITValuePublisher
{
    private readonly int _period;
    private readonly double _decayLambda;
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly double[] _hBuf_prev;
    private readonly double[] _lBuf_prev;

    private int _count;
    private long _index;
    private double _currentMax;
    private double _currentMin;
    private long _maxAge;
    private long _minAge;
    private double _rawMax;
    private double _rawMin;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValidHigh,
        double LastValidLow,
        double CurrentMax,
        double CurrentMin,
        long MaxAge,
        long MinAge,
        double RawMax,
        double RawMin,
        int Count,
        long Index);

    private State _state;
    private State _p_state;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Upper { get; private set; }
    public TValue Lower { get; private set; }
    public bool IsHot => _count >= _period;

    public event TValuePublishedHandler? Pub;

    public Decaychannel(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _decayLambda = Math.Log(2.0) / period;
        _hBuf = new double[_period];
        _lBuf = new double[_period];
        _hBuf_prev = new double[_period];
        _lBuf_prev = new double[_period];
        _count = 0;
        _index = -1;
        _currentMax = double.NaN;
        _currentMin = double.NaN;
        _rawMax = double.NaN;
        _rawMin = double.NaN;
        _maxAge = 0;
        _minAge = 0;

        _state = new State(double.NaN, double.NaN, double.NaN, double.NaN, 0, 0, double.NaN, double.NaN, 0, -1);
        _p_state = _state;

        Name = $"Decaychannel({period})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    public Decaychannel(TBarSeries source, int period) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) => Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double high, double low) GetValid(double high, double low)
    {
        if (double.IsFinite(high))
            _state = _state with { LastValidHigh = high };
        else
            high = _state.LastValidHigh;

        if (double.IsFinite(low))
            _state = _state with { LastValidLow = low };
        else
            low = _state.LastValidLow;

        return (high, low);
    }

    private void SaveState()
    {
        _state = new State(
            _state.LastValidHigh,
            _state.LastValidLow,
            _currentMax,
            _currentMin,
            _maxAge,
            _minAge,
            _rawMax,
            _rawMin,
            _count,
            _index);

        // Save buffer contents
        Array.Copy(_hBuf, _hBuf_prev, _period);
        Array.Copy(_lBuf, _lBuf_prev, _period);
    }

    private void RestoreState()
    {
        _currentMax = _p_state.CurrentMax;
        _currentMin = _p_state.CurrentMin;
        _maxAge = _p_state.MaxAge;
        _minAge = _p_state.MinAge;
        _rawMax = _p_state.RawMax;
        _rawMin = _p_state.RawMin;
        _count = _p_state.Count;
        _index = _p_state.Index;
        _state = _p_state;

        // Restore buffer contents
        Array.Copy(_hBuf_prev, _hBuf, _period);
        Array.Copy(_lBuf_prev, _lBuf, _period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double rawMax, double rawMin) ComputeRawExtremes()
    {
        int len = Math.Min(_count, _period);
        if (len == 0)
            return (double.NaN, double.NaN);

        double max = double.MinValue;
        double min = double.MaxValue;

        for (int i = 0; i < len; i++)
        {
            int idx = (int)((_index - i) % _period);
            if (idx < 0) idx += _period;

            double h = _hBuf[idx];
            double l = _lBuf[idx];

            if (h > max) max = h;
            if (l < min) min = l;
        }

        return (max, min);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            // Save state BEFORE advancing (this is state from end of previous bar)
            SaveState();
            _p_state = _state;

            // Now advance to new bar
            _index++;
            if (_count < _period)
                _count++;
        }
        else
        {
            // Restore to state before current bar
            RestoreState();

            // Re-advance to current bar position (we're reprocessing current bar)
            _index++;
            if (_count < _period)
                _count++;
        }

        int bufIdx = (int)(_index % _period);
        var (high, low) = GetValid(input.High, input.Low);

        if (double.IsNaN(high) || double.IsNaN(low))
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = new TValue(input.Time, double.NaN);
            Lower = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        // Get raw max/min over the period window
        var (rawMax, rawMin) = ComputeRawExtremes();
        _rawMax = rawMax;
        _rawMin = rawMin;

        // Check if new extremes
        bool newMax = high >= rawMax;
        bool newMin = low <= rawMin;

        // Same logic for both isNew=true and isNew=false (correction reprocesses identically)
        if (newMax)
        {
            _currentMax = rawMax;
            _maxAge = 0;
        }
        else
        {
            _maxAge++;
        }

        if (newMin)
        {
            _currentMin = rawMin;
            _minAge = 0;
        }
        else
        {
            _minAge++;
        }

        // Initialize if first valid values
        if (double.IsNaN(_currentMax))
        {
            _currentMax = rawMax;
            _maxAge = 0;
        }
        if (double.IsNaN(_currentMin))
        {
            _currentMin = rawMin;
            _minAge = 0;
        }

        // Apply decay toward midpoint
        double midpoint = (_currentMax + _currentMin) * 0.5;

        // decayRate = 1 - e^(-lambda * age)
        double maxDecayRate = 1.0 - Math.Exp(-_decayLambda * _maxAge);
        double minDecayRate = 1.0 - Math.Exp(-_decayLambda * _minAge);

        // Apply decay: currentMax = currentMax - decayRate * (currentMax - midpoint)
        // Using FMA: currentMax = midpoint + (1 - decayRate) * (currentMax - midpoint)
        double decayedMax = Math.FusedMultiplyAdd(1.0 - maxDecayRate, _currentMax - midpoint, midpoint);
        double decayedMin = Math.FusedMultiplyAdd(1.0 - minDecayRate, _currentMin - midpoint, midpoint);

        // Constrain within raw extremes
        double top = Math.Min(decayedMax, rawMax);
        double bot = Math.Max(decayedMin, rawMin);

        // Update tracked values for next iteration
        _currentMax = Math.Max(top, rawMax);
        _currentMin = Math.Min(bot, rawMin);

        double mid = (top + bot) * 0.5;

        Last = new TValue(input.Time, mid);
        Upper = new TValue(input.Time, top);
        Lower = new TValue(input.Time, bot);

        PubEvent(Last, isNew);
        return Last;
    }

    public (TSeries Middle, TSeries Upper, TSeries Lower) Update(TBarSeries source)
    {
        if (source.Count == 0)
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));

        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        var tSpan = CollectionsMarshal.AsSpan(tMiddle);
        var vMiddleSpan = CollectionsMarshal.AsSpan(vMiddle);
        var vUpperSpan = CollectionsMarshal.AsSpan(vUpper);
        var vLowerSpan = CollectionsMarshal.AsSpan(vLower);

        Batch(source.HighValues, source.LowValues, vMiddleSpan, vUpperSpan, vLowerSpan, _period);

        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, vMiddleSpan[^1]);
        Upper = new TValue(lastTime, vUpperSpan[^1]);
        Lower = new TValue(lastTime, vLowerSpan[^1]);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    public void Prime(TBarSeries source)
    {
        Reset();

        if (source.Count == 0)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    public void Reset()
    {
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        Array.Clear(_hBuf_prev);
        Array.Clear(_lBuf_prev);
        _count = 0;
        _index = -1;
        _currentMax = double.NaN;
        _currentMin = double.NaN;
        _rawMax = double.NaN;
        _rawMin = double.NaN;
        _maxAge = 0;
        _minAge = 0;
        _state = new State(double.NaN, double.NaN, double.NaN, double.NaN, 0, 0, double.NaN, double.NaN, 0, -1);
        _p_state = _state;
        Last = default;
        Upper = default;
        Lower = default;
    }

    /// <summary>
    /// Batch calculation using spans (zero allocation except ArrayPool rentals).
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (high.Length != low.Length)
            throw new ArgumentException("High and Low spans must have the same length", nameof(high));
        if (middle.Length < high.Length || upper.Length < high.Length || lower.Length < high.Length)
            throw new ArgumentException("Output spans must be at least as long as inputs", nameof(middle));

        int len = high.Length;
        if (len == 0) return;

        double decayLambda = Math.Log(2.0) / period;

        // Compute raw rolling max/min
        double[] rawMaxArr = ArrayPool<double>.Shared.Rent(len);
        double[] rawMinArr = ArrayPool<double>.Shared.Rent(len);

        try
        {
            QuanTAlib.Highest.Calculate(high, rawMaxArr.AsSpan(0, len), period);
            QuanTAlib.Lowest.Calculate(low, rawMinArr.AsSpan(0, len), period);

            double currentMax = double.NaN;
            double currentMin = double.NaN;
            long maxAge = 0;
            long minAge = 0;

            for (int i = 0; i < len; i++)
            {
                double rawMax = rawMaxArr[i];
                double rawMin = rawMinArr[i];
                double h = high[i];
                double l = low[i];

                bool newMax = h >= rawMax;
                bool newMin = l <= rawMin;

                if (newMax || double.IsNaN(currentMax))
                {
                    currentMax = rawMax;
                    maxAge = 0;
                }
                else
                {
                    maxAge++;
                }

                if (newMin || double.IsNaN(currentMin))
                {
                    currentMin = rawMin;
                    minAge = 0;
                }
                else
                {
                    minAge++;
                }

                double midpoint = (currentMax + currentMin) * 0.5;

                double maxDecayRate = 1.0 - Math.Exp(-decayLambda * maxAge);
                double minDecayRate = 1.0 - Math.Exp(-decayLambda * minAge);

                double decayedMax = Math.FusedMultiplyAdd(1.0 - maxDecayRate, currentMax - midpoint, midpoint);
                double decayedMin = Math.FusedMultiplyAdd(1.0 - minDecayRate, currentMin - midpoint, midpoint);

                double top = Math.Min(decayedMax, rawMax);
                double bot = Math.Max(decayedMin, rawMin);

                currentMax = Math.Max(top, rawMax);
                currentMin = Math.Min(bot, rawMin);

                double mid = (top + bot) * 0.5;

                middle[i] = mid;
                upper[i] = top;
                lower[i] = bot;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rawMaxArr);
            ArrayPool<double>.Shared.Return(rawMinArr);
        }
    }

    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period)
    {
        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        Batch(source.HighValues, source.LowValues,
            CollectionsMarshal.AsSpan(vMiddle),
            CollectionsMarshal.AsSpan(vUpper),
            CollectionsMarshal.AsSpan(vLower),
            period);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tMiddle));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tUpper));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tLower));

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Decaychannel Indicator) Calculate(TBarSeries source, int period)
    {
        var indicator = new Decaychannel(source, period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
