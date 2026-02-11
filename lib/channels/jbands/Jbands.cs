using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// JBANDS: Jurik Adaptive Envelope Bands
/// Upper and Lower bands from JMA's internal adaptive envelope tracking.
/// These bands snap to new extremes instantly but decay smoothly toward price,
/// creating volatility-responsive channels with JMA's signature smoothness.
/// Middle band is the JMA smoothed value itself.
/// </summary>
[SkipLocalsInit]
public sealed class Jbands : ITValuePublisher, IDisposable
{
    private const int VolWindowSize = 128;
    private const int DevWindowSize = 10;
    private const int JurikTrimCount = 65;

    // Jurik core parameters
    private readonly double _phaseParam;
    private readonly double _logParam;
    private readonly double _lengthDivider;
    private readonly double _logSqrtDivider;
    private readonly double _logLengthDivider;
    private readonly double _pExponent;

    // Buffers
    private readonly RingBuffer _devBuffer;
    private readonly RingBuffer _volBuffer;
    private readonly TValuePublishedHandler _handler;

    // Subscription tracking for IDisposable
    private ITValuePublisher? _source;
    private bool _disposed;

    // Streaming state
    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double UpperBand;
        public double LowerBand;
        public double LastC0;
        public double LastC8;
        public double LastA8;
        public double LastJma;
        public double LastPrice;
        public int Bars;
    }

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Upper { get; private set; }
    public TValue Lower { get; private set; }
    public bool IsHot => _state.Bars >= WarmupPeriod;

    public event TValuePublishedHandler? Pub;

    public Jbands(int period, int phase = 0, double power = 0.45)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        if (!double.IsFinite(power))
        {
            throw new ArgumentException("Power must be finite.", nameof(power));
        }

        // Phase parameter: maps -100..100 -> 0.5..2.5
        if (phase < -100)
        {
            _phaseParam = 0.5;
        }
        else if (phase > 100)
        {
            _phaseParam = 2.5;
        }
        else
        {
            _phaseParam = (phase * 0.01) + 1.5;
        }

        // Length / log / divider parameters from decompiled JMA
        double lengthParam = period < 1.0000000002
            ? 0.0000000001
            : (period - 1.0) / 2.0;

        double logParam = Math.Log(Math.Sqrt(lengthParam)) / Math.Log(2.0);
        logParam = (logParam + 2.0) < 0.0 ? 0.0 : (logParam + 2.0);
        _logParam = logParam;
        _pExponent = Math.Max(_logParam - 2.0, 0.5);

        double sqrtParam = Math.Sqrt(lengthParam) * _logParam;
        lengthParam *= 0.9;
        _lengthDivider = lengthParam / (lengthParam + 2.0);
        double sqrtDivider = sqrtParam / (sqrtParam + 1.0);

        _logLengthDivider = Math.Log(Math.Max(_lengthDivider, 1e-12));
        _logSqrtDivider = Math.Log(Math.Max(sqrtDivider, 1e-12));

        WarmupPeriod = (int)Math.Ceiling(20.0 + 80.0 * Math.Pow(period, 0.36));

        _handler = Handle;
        Name = $"Jbands({period},{phase},{power})";

        _devBuffer = new RingBuffer(DevWindowSize);
        _volBuffer = new RingBuffer(VolWindowSize);

        Reset();
    }

    public Jbands(ITValuePublisher source, int period, int phase = 0, double power = 0.45)
        : this(period, phase, power)
    {
        _source = source;
        source.Pub += _handler;
    }

    /// <summary>
    /// Releases the event subscription to the source publisher.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_source != null)
        {
            _source.Pub -= _handler;
            _source = null;
        }

        _disposed = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = default;
        _p_state = default;
        _devBuffer.Clear();
        _volBuffer.Clear();
        Last = default;
        Upper = default;
        Lower = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double jma, double upper, double lower) Step(double value, bool isNew)
    {
        HandleStateSnapshot(isNew);
        if (!double.IsFinite(value))
        {
            if (_state.Bars == 0)
            {
                return (double.NaN, double.NaN, double.NaN);
            }

            value = _state.LastPrice;
        }
        else
        {
            _state.LastPrice = value;
        }

        _state.Bars++;
        if (_state.Bars == 1)
        {
            return InitializeFirstBar(value);
        }

        return CalculateJbands(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleStateSnapshot(bool isNew)
    {
        if (isNew)
        {
            _p_state = _state;
            _devBuffer.Snapshot();
            _volBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _devBuffer.Restore();
            _volBuffer.Restore();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double jma, double upper, double lower) InitializeFirstBar(double value)
    {
        _state.UpperBand = value;
        _state.LowerBand = value;
        _state.LastC0 = value;
        _state.LastC8 = 0.0;
        _state.LastA8 = 0.0;
        _state.LastJma = value;
        return (value, value, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double jma, double upper, double lower) CalculateJbands(double value)
    {
        // 1. Local deviation
        double diffA = value - _state.UpperBand;
        double diffB = value - _state.LowerBand;
        double absA = Math.Abs(diffA);
        double absB = Math.Abs(diffB);
        double absValue = absA > absB ? absA : absB;
        double deviation = absValue + 1e-10;

        // 2. 10-bar SMA of local deviation
        _devBuffer.Add(deviation);
        double volatility = _devBuffer.Average;

        // 3. 128-bar volatility history + trimmed mean
        _volBuffer.Add(volatility);
        double refVolatility = CalculateTrimmedMean(volatility);
        refVolatility = refVolatility <= 0.0 ? deviation : refVolatility;

        // 4. Jurik dynamic exponent
        double d = CalculateJurikExponent(absValue, refVolatility);

        // 5. Update bands
        UpdateBands(value, d);

        // 6. IIR filter for JMA (middle band)
        double jma = CalculateIIRFilter(value, d);

        return (jma, _state.UpperBand, _state.LowerBand);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateJurikExponent(double absValue, double refVolatility)
    {
        double ratio = Math.Max(absValue / refVolatility, 0.0);
        double d = Math.Pow(ratio, _pExponent);
        if (d > _logParam)
        {
            d = _logParam;
        }

        if (d < 1.0)
        {
            d = 1.0;
        }

        return d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateBands(double value, double d)
    {
        double adapt = Math.Exp(_logSqrtDivider * Math.Sqrt(d));
        _state.UpperBand = (value > _state.UpperBand)
            ? value
            : Math.FusedMultiplyAdd(adapt, _state.UpperBand - value, value);
        _state.LowerBand = (value < _state.LowerBand)
            ? value
            : Math.FusedMultiplyAdd(adapt, _state.LowerBand - value, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateIIRFilter(double value, double d)
    {
        double prevJma = double.IsNaN(_state.LastJma) ? value : _state.LastJma;

        double alpha = Math.Exp(_logLengthDivider * d);
        double decay = 1.0 - alpha;
        double alpha2 = alpha * alpha;

        double c0 = Math.FusedMultiplyAdd(_state.LastC0, alpha, decay * value);
        double lengthDecay = 1.0 - _lengthDivider;
        double c8 = Math.FusedMultiplyAdd(_state.LastC8, _lengthDivider, lengthDecay * (value - c0));
        double coef = Math.FusedMultiplyAdd(alpha, -2.0, alpha2 + 1.0);
        double a8 = Math.FusedMultiplyAdd(_state.LastA8, alpha2, Math.FusedMultiplyAdd(_phaseParam, c8, c0 - prevJma) * coef);

        double jma = prevJma + a8;

        _state.LastC0 = c0;
        _state.LastC8 = c8;
        _state.LastA8 = a8;
        _state.LastJma = jma;

        return jma;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        var (jma, upper, lower) = Step(input.Value, isNew);
        Last = new TValue(input.Time, jma);
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

        source.Times.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            var (jma, upper, lower) = Step(source.Values[i], isNew: true);
            vMiddleSpan[i] = jma;
            vUpperSpan[i] = upper;
            vLowerSpan[i] = lower;
        }

        _p_state = _state;
        _devBuffer.Snapshot();
        _volBuffer.Snapshot();

        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, vMiddleSpan[^1]);
        Upper = new TValue(lastTime, vUpperSpan[^1]);
        Lower = new TValue(lastTime, vLowerSpan[^1]);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public void Prime(TSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(new TValue(new DateTime(source.Times[i], DateTimeKind.Utc), source.Values[i]), isNew: true);
        }
    }

    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TSeries source, int period, int phase = 0, double power = 0.45)
    {
        var jbands = new Jbands(period, phase, power);
        return jbands.Update(source);
    }

    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        int phase = 0,
        double power = 0.45)
    {
        if (middle.Length != source.Length)
        {
            throw new ArgumentException("Source and middle must have the same length.", nameof(middle));
        }

        if (upper.Length != source.Length)
        {
            throw new ArgumentException("Source and upper must have the same length.", nameof(upper));
        }

        if (lower.Length != source.Length)
        {
            throw new ArgumentException("Source and lower must have the same length.", nameof(lower));
        }

        if (source.Length == 0)
        {
            return;
        }

        var jbands = new Jbands(period, phase, power);
        for (int i = 0; i < source.Length; i++)
        {
            var (jma, u, l) = jbands.Step(source[i], isNew: true);
            middle[i] = jma;
            upper[i] = u;
            lower[i] = l;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateTrimmedMean(double fallback)
    {
        int count = _volBuffer.Count;
        if (count < 16)
        {
            return fallback;
        }

        Span<double> sorted = stackalloc double[count];
        _volBuffer.CopyTo(sorted);
        sorted.Sort();

        int start, end;
        if (count >= VolWindowSize)
        {
            int leftSkip = (int)Math.Ceiling((VolWindowSize - JurikTrimCount) / 2.0);
            start = leftSkip;
            end = start + JurikTrimCount - 1;
        }
        else
        {
            int slice = (int)Math.Max(5, Math.Round(count * 0.5));
            int drop = (count - slice) / 2;
            start = drop;
            end = drop + slice - 1;
        }

        if (start < 0)
        {
            start = 0;
        }

        if (end >= count)
        {
            end = count - 1;
        }

        int len = end - start + 1;
        return sorted.Slice(start, len).SumSIMD() / len;
    }
}
