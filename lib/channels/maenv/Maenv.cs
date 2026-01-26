using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MA Type enumeration for MAENV indicator.
/// </summary>
public enum MaenvType
{
    /// <summary>Simple Moving Average (O(1) with ring buffer)</summary>
    SMA = 0,
    /// <summary>Exponential Moving Average (O(1) with warmup)</summary>
    EMA = 1,
    /// <summary>Weighted Moving Average (O(n))</summary>
    WMA = 2
}

/// <summary>
/// MAENV: Moving Average Envelope
/// A percentage-based envelope using a selectable moving average as the middle line.
/// Middle = MA(source, period) - SMA, EMA, or WMA
/// Upper = Middle + (Middle × percentage / 100)
/// Lower = Middle - (Middle × percentage / 100)
/// </summary>
[SkipLocalsInit]
public sealed class Maenv : ITValuePublisher
{
    private readonly int _period;
    private readonly double _percentage;
    private readonly MaenvType _maType;
    private readonly double _emaAlpha;

    // Ring buffer for SMA
    private readonly double[]? _smaBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        // EMA state
        double EmaSum,
        double EmaWeight,
        // SMA state
        double SmaSum,
        int SmaHead,
        int SmaCount,
        // WMA state
        int WmaCount,
        // General
        double LastValid,
        int Bars,
        bool IsHot);

    private State _state;
    private State _p_state;
    private double[]? _p_smaBuffer;

    // WMA lookback buffer
    private readonly double[]? _wmaBuffer;
    private double[]? _p_wmaBuffer;

    private readonly TValuePublishedHandler _valueHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Upper { get; private set; }
    public TValue Lower { get; private set; }
    public bool IsHot => _state.IsHot;

    public event TValuePublishedHandler? Pub;

    public Maenv(int period = 20, double percentage = 1.0, MaenvType maType = MaenvType.EMA)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        if (percentage <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be > 0.");
        }

        _period = period;
        _percentage = percentage;
        _maType = maType;
        _emaAlpha = 2.0 / (period + 1);

        WarmupPeriod = period;

        Name = $"Maenv({period},{percentage},{maType})";
        _valueHandler = HandleValue;

        // Allocate buffers based on MA type
        if (maType == MaenvType.SMA)
        {
            _smaBuffer = new double[period];
            _p_smaBuffer = new double[period];
        }
        else if (maType == MaenvType.WMA)
        {
            _wmaBuffer = new double[period];
            _p_wmaBuffer = new double[period];
        }

        Reset();
    }

    public Maenv(TSeries source, int period = 20, double percentage = 1.0, MaenvType maType = MaenvType.EMA) : this(period, percentage, maType)
    {
        Prime(source);
        source.Pub += _valueHandler;
    }

    private void HandleValue(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = new State(0, 0, 0, 0, 0, 0, double.NaN, 0, false);
        _p_state = _state;

        if (_smaBuffer != null)
        {
            Array.Fill(_smaBuffer, 0.0);
            _p_smaBuffer = (double[])_smaBuffer.Clone();
        }

        if (_wmaBuffer != null)
        {
            Array.Fill(_wmaBuffer, 0.0);
            _p_wmaBuffer = (double[])_wmaBuffer.Clone();
        }

        Last = default;
        Upper = default;
        Lower = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValid(double value, bool isNew)
    {
        if (double.IsFinite(value))
        {
            if (isNew)
            {
                _state = _state with { LastValid = value };
            }

            return value;
        }
        return _state.LastValid;
    }

    // ========================
    // Update overloads (adjacent per S4136)
    // ========================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            if (_smaBuffer != null && _p_smaBuffer != null)
            {
                Array.Copy(_smaBuffer, _p_smaBuffer, _period);
            }

            if (_wmaBuffer != null && _p_wmaBuffer != null)
            {
                Array.Copy(_wmaBuffer, _p_wmaBuffer, _period);
            }
        }
        else
        {
            _state = _p_state;
            if (_smaBuffer != null && _p_smaBuffer != null)
            {
                Array.Copy(_p_smaBuffer, _smaBuffer, _period);
            }

            if (_wmaBuffer != null && _p_wmaBuffer != null)
            {
                Array.Copy(_p_wmaBuffer, _wmaBuffer, _period);
            }
        }

        double value = GetValid(input.Value, isNew);

        if (isNew)
        {
            _state = _state with { Bars = _state.Bars + 1 };
        }

        double middle = _maType switch
        {
            MaenvType.SMA => CalculateSMA(value, isNew),
            MaenvType.EMA => CalculateEMA(value, isNew),
            MaenvType.WMA => CalculateWMA(value, isNew),
            _ => value
        };

        double dist = middle * _percentage / 100.0;
        double upper = middle + dist;
        double lower = middle - dist;

        if (!_state.IsHot && _state.Bars >= WarmupPeriod)
        {
            _state = _state with { IsHot = true };
        }

        Last = new TValue(input.Time, middle);
        Upper = new TValue(input.Time, upper);
        Lower = new TValue(input.Time, lower);

        PubEvent(Last, isNew);
        return Last;
    }

    public (TSeries Middle, TSeries Upper, TSeries Lower) Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));
        }

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

        Batch(source.Values, vMiddleSpan, vUpperSpan, vLowerSpan, _period, _percentage, _maType);

        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, vMiddleSpan[^1]);
        Upper = new TValue(lastTime, vUpperSpan[^1]);
        Lower = new TValue(lastTime, vLowerSpan[^1]);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    // ========================
    // Private MA calculation helpers
    // ========================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateSMA(double value, bool isNew)
    {
        if (_smaBuffer == null)
        {
            return value;
        }

        // Calculate new count (always increment if not full, for both isNew cases)
        int currentCount = _state.SmaCount;
        int calcCount = currentCount < _period ? currentCount + 1 : currentCount;

        // Remove oldest value from sum if buffer is full
        double oldest = _smaBuffer[_state.SmaHead];
        double newSum = _state.SmaSum;

        if (currentCount >= _period)
        {
            newSum -= oldest;
        }

        // Add new value
        newSum += value;

        // Update buffer
        _smaBuffer[_state.SmaHead] = value;
        int newHead = (_state.SmaHead + 1) % _period;

        // Persist state only for isNew=true
        if (isNew)
        {
            _state = _state with
            {
                SmaSum = newSum,
                SmaHead = newHead,
                SmaCount = calcCount
            };
        }

        return newSum / calcCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateEMA(double value, bool isNew)
    {
        // Use EmaWeight==0 to detect first value for correct isNew=false behavior
        if (Math.Abs(_state.EmaWeight) < double.Epsilon)
        {
            // First value - persist only for isNew=true
            if (isNew)
            {
                _state = _state with
                {
                    EmaSum = value,
                    EmaWeight = 1.0
                };
            }
            return value;
        }

        // EMA with warmup compensation
        double newSum = Math.FusedMultiplyAdd(_state.EmaSum, 1.0 - _emaAlpha, value * _emaAlpha);
        double newWeight = Math.FusedMultiplyAdd(_state.EmaWeight, 1.0 - _emaAlpha, _emaAlpha);

        // Persist state only for isNew=true
        if (isNew)
        {
            _state = _state with
            {
                EmaSum = newSum,
                EmaWeight = newWeight
            };
        }

        return newSum / newWeight;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWMA(double value, bool isNew)
    {
        if (_wmaBuffer == null)
        {
            return value;
        }

        // Calculate count for this bar (always increment if not full, for both isNew cases)
        int currentCount = _state.WmaCount;
        int calcCount = currentCount < _period ? currentCount + 1 : currentCount;

        // Shift buffer (always shift if count > 1, regardless of isNew)
        // This ensures restoration produces same buffer state as original
        if (calcCount > 1)
        {
            for (int i = _period - 1; i > 0; i--)
            {
                _wmaBuffer[i] = _wmaBuffer[i - 1];
            }
        }
        _wmaBuffer[0] = value;

        // Persist state only for isNew=true
        if (isNew)
        {
            _state = _state with { WmaCount = calcCount };
        }

        // Calculate WMA
        double norm = 0.0;
        double sum = 0.0;

        for (int i = 0; i < calcCount; i++)
        {
            double w = (_period - i) * _period;
            norm += w;
            sum += _wmaBuffer[i] * w;
        }

        return norm > 0 ? sum / norm : value;
    }

    public void Prime(TSeries source)
    {
        Reset();

        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    // ========================
    // Batch overloads (adjacent per S4136)
    // ========================

    /// <summary>
    /// Batch calculation using spans (zero allocation for SMA and EMA).
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double percentage = 1.0,
        MaenvType maType = MaenvType.EMA)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        if (percentage <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be > 0.");
        }

        if (middle.Length < source.Length || upper.Length < source.Length || lower.Length < source.Length)
        {
            throw new ArgumentException("Output spans must be at least as long as input", nameof(middle));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        switch (maType)
        {
            case MaenvType.SMA:
                BatchSMA(source, middle, upper, lower, period, percentage);
                break;
            case MaenvType.EMA:
                BatchEMA(source, middle, upper, lower, period, percentage);
                break;
            case MaenvType.WMA:
                BatchWMA(source, middle, upper, lower, period, percentage);
                break;
        }
    }

    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TSeries source, int period = 20, double percentage = 1.0, MaenvType maType = MaenvType.EMA)
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

        Batch(source.Values,
              CollectionsMarshal.AsSpan(vMiddle),
              CollectionsMarshal.AsSpan(vUpper),
              CollectionsMarshal.AsSpan(vLower),
              period, percentage, maType);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tMiddle));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tUpper));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tLower));

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    // ========================
    // Private batch helpers
    // ========================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BatchSMA(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double percentage)
    {
        int len = source.Length;
        Span<double> buffer = period <= 256 ? stackalloc double[period] : new double[period];
        buffer.Clear();

        double sum = 0.0;
        int head = 0;
        int count = 0;

        for (int i = 0; i < len; i++)
        {
            double value = source[i];

            // Remove oldest if full
            if (count >= period)
            {
                sum -= buffer[head];
            }
            else
            {
                count++;
            }

            // Add new
            sum += value;
            buffer[head] = value;
            head = (head + 1) % period;

            double ma = sum / count;
            double dist = ma * percentage / 100.0;
            middle[i] = ma;
            upper[i] = ma + dist;
            lower[i] = ma - dist;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BatchEMA(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double percentage)
    {
        int len = source.Length;
        double alpha = 2.0 / (period + 1);
        double emaSum = source[0];
        double emaWeight = 1.0;

        double ma = emaSum;
        double dist = ma * percentage / 100.0;
        middle[0] = ma;
        upper[0] = ma + dist;
        lower[0] = ma - dist;

        for (int i = 1; i < len; i++)
        {
            double value = source[i];

            emaSum = Math.FusedMultiplyAdd(emaSum, 1.0 - alpha, value * alpha);
            emaWeight = Math.FusedMultiplyAdd(emaWeight, 1.0 - alpha, alpha);

            ma = emaSum / emaWeight;
            dist = ma * percentage / 100.0;
            middle[i] = ma;
            upper[i] = ma + dist;
            lower[i] = ma - dist;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BatchWMA(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double percentage)
    {
        int len = source.Length;

        for (int i = 0; i < len; i++)
        {
            double norm = 0.0;
            double sum = 0.0;
            int count = Math.Min(i + 1, period);

            for (int j = 0; j < count; j++)
            {
                double w = (period - j) * period;
                norm += w;
                sum += source[i - j] * w;
            }

            double ma = norm > 0 ? sum / norm : source[i];
            double dist = ma * percentage / 100.0;
            middle[i] = ma;
            upper[i] = ma + dist;
            lower[i] = ma - dist;
        }
    }

    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Maenv Indicator) Calculate(TSeries source, int period = 20, double percentage = 1.0, MaenvType maType = MaenvType.EMA)
    {
        var indicator = new Maenv(source, period, percentage, maType);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
