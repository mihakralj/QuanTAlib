using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Jurik Moving Average (JMA):
/// - 10-bar SMA of local deviation
/// - 128-sample volatility distribution
/// - middle-65 trimmed mean as volatility reference
/// - Jurik dynamic exponent and 2-pole IIR core
/// - power parameter kept for API compatibility; ignored (matches Pine reference)
/// </summary>
[SkipLocalsInit]
public sealed class Jma : AbstractBase
{
    private const int VolWindowSize = 128; // volatility history length
    private const int DevWindowSize = 10;  // short SMA length for deviation

    // Jurik core parameters derived from period/phase
    private readonly double _phaseParam;     // 0.5 .. 2.5
    private readonly double _logParam;       // log(sqrt(L))/log(2) + 2, clamped >= 0
    private readonly double _lengthDivider;  // L'/(L'+2), L' = 0.9*L
    private readonly double _logSqrtDivider; // Precomputed log(_sqrtDivider) for Exp optimization
    private readonly double _logLengthDivider; // Precomputed log(_lengthDivider) for Exp optimization
    private readonly double _pExponent;      // max(logParam - 2, 0.5)

    // Constants for trimmed mean
    private const int JurikTrimCount = 65; // canonical JMA: middle 65 of 128 samples

    // Buffers
    private readonly RingBuffer _devBuffer;
    private readonly RingBuffer _volBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;

    // Streaming state (current + previous snapshot for isNew=false)
    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        // Jurik "envelope" anchors
        public double UpperBand;
        public double LowerBand;

        // IIR filter internal state
        public double LastC0;
        public double LastC8;
        public double LastA8;
        public double LastJma;

        // last finite price (for NaN handling)
        public double LastPrice;

        // counters
        public int Bars;
    }

    public override bool IsHot => _state.Bars >= WarmupPeriod;

    public Jma(int period, int phase = 0, double power = 0.45)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        if (!double.IsFinite(power))
            throw new ArgumentException("Power must be finite.", nameof(power));

        // --- Phase parameter: maps -100..100 -> 0.5..2.5 (Jurik convention) ---
        if (phase < -100)
            _phaseParam = 0.5;
        else if (phase > 100)
            _phaseParam = 2.5;
        else
            _phaseParam = (phase * 0.01) + 1.5;

        // --- Length / log / divider parameters (from decompiled JMA) ---
        // L_raw ~ (period - 1)/2, with a tiny lower bound to avoid log(0)
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

        // Precompute logs for Math.Exp optimization
        // Clamp to avoid -Infinity when period=1 (dividers can be zero)
        _logLengthDivider = Math.Log(Math.Max(_lengthDivider, 1e-12));
        _logSqrtDivider = Math.Log(Math.Max(sqrtDivider, 1e-12));

        // same warmup heuristic used in the AFL port (SetBarsRequired)
        WarmupPeriod = (int)Math.Ceiling(20.0 + 80.0 * Math.Pow(period, 0.36));

        _handler = Handle;
        Name = $"Jma({period},{phase},{power})"; // power kept for signature compatibility (ignored in calculation)

        _devBuffer = new RingBuffer(DevWindowSize);
        _volBuffer = new RingBuffer(VolWindowSize);

        Reset();
    }

    public Jma(ITValuePublisher source, int period, int phase = 0, double power = 0.45)
        : this(period, phase, power)
    {
        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _state = default;
        _p_state = default;
        _devBuffer.Clear();
        _volBuffer.Clear();
        Last = default;
    }

    /// <summary>
    /// Core streaming step: feed a single value, get JMA.
    /// Honors isNew semantics by snapshotting state+buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Step(double value, bool isNew)
    {
        HandleStateSnapshot(isNew);
        if (!double.IsFinite(value))
        {
            if (_state.Bars == 0)
                return double.NaN;
            value = _state.LastPrice;
        }
        else
        {
            _state.LastPrice = value;
        }

        _state.Bars++;
        if (_state.Bars == 1)
            return InitializeFirstBar(value);

        return CalculateJma(value);
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
    private double InitializeFirstBar(double value)
    {
        _state.UpperBand = value;
        _state.LowerBand = value;
        _state.LastC0 = value;
        _state.LastC8 = 0.0;
        _state.LastA8 = 0.0;
        _state.LastJma = value;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateJma(double value)
    {
        // 1. Local deviation: |price - {UpperBand, LowerBand}|
        double diffA = value - _state.UpperBand;
        double diffB = value - _state.LowerBand;
        double absA = Math.Abs(diffA);
        double absB = Math.Abs(diffB);
        double absValue = absA > absB ? absA : absB;
        double deviation = absValue + 1e-10;

        // 2. 10-bar SMA of local deviation -> "volatility"
        _devBuffer.Add(deviation);
        double volatility = _devBuffer.Average;

        // 3. 128-bar volatility history + middle-65 trimmed mean
        _volBuffer.Add(volatility);
        double refVolatility = CalculateTrimmedMean(volatility);
        refVolatility = refVolatility <= 0.0 ? deviation : refVolatility;

        // 4. Jurik dynamic exponent d from abs/refVolatility
        double d = CalculateJurikExponent(absValue, refVolatility);

        // 5. Update UpperBand / LowerBand using sqrtDivider ^ sqrt(d)
        UpdateBands(value, d);

        // 6. 2-pole IIR core using d as the "speed"
        return CalculateIIRFilter(value, d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateJurikExponent(double absValue, double refVolatility)
    {
        double ratio = Math.Max(absValue / refVolatility, 0.0);
        double d = Math.Pow(ratio, _pExponent);
        if (d > _logParam) d = _logParam;
        if (d < 1.0) d = 1.0;
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

        // EMA smoothing: c0 = decay * value + alpha * LastC0
        double c0 = Math.FusedMultiplyAdd(_state.LastC0, alpha, decay * value);
        // EMA smoothing: c8 = (value - c0) * (1 - lengthDivider) + lengthDivider * LastC8
        double lengthDecay = 1.0 - _lengthDivider;
        double c8 = Math.FusedMultiplyAdd(_state.LastC8, _lengthDivider, lengthDecay * (value - c0));
        // IIR filter: a8 = (phase * c8 + c0 - prevJma) * coef + alpha2 * LastA8
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
    public override TValue Update(TValue input, bool isNew = true)
    {
        double j = Step(input.Value, isNew);
        Last = new TValue(input.Time, j);
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

        source.Times.CopyTo(tSpan);

        // Reset and calculate in a single pass.
        // The IIR filter state after processing the full series is mathematically correct.
        // No need for a second replay - that would truncate the infinite impulse response
        // and actually reduce precision.
        Reset();
        for (int i = 0; i < len; i++)
        {
            vSpan[i] = Step(source.Values[i], isNew: true);
        }

        // Synchronize previous-state mirror to current state AND snapshot buffers
        // so subsequent streaming Update calls with isNew=false will use correct _p_state
        _p_state = _state;
        _devBuffer.Snapshot();
        _volBuffer.Snapshot();

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    protected override void Dispose(bool disposing)
    {
        if (disposing && _source != null)
        {
            _source.Pub -= _handler;
        }
        base.Dispose(disposing);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period, int phase = 0, double power = 0.45)
    {
        var jma = new Jma(period, phase, power);
        return jma.Update(source);
    }

    /// <summary>
    /// Static helper compatible with your existing signature.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source,
                                 Span<double> output,
                                 int period,
                                 int phase = 0,
                                 double power = 0.45)
    {
        if (output.Length != source.Length)
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        if (source.Length == 0)
            return;

        var jma = new Jma(period, phase, power);
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = jma.Step(source[i], isNew: true);
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

        // Stack-allocate scratch buffer for sorting (max 128 * 8 bytes = 1KB)
        // This eliminates the heap-allocated _sorted field and improves cache locality
        Span<double> sorted = stackalloc double[count];
        _volBuffer.CopyTo(sorted);
        sorted.Sort();

        int start, end;
        if (count >= VolWindowSize)
        {
            // canonical JMA: central 65 of 128 -> indices 32..96
            // Approximately removes the outer 25% on each tail
            int leftSkip = (int)Math.Ceiling((VolWindowSize - JurikTrimCount) / 2.0);
            start = leftSkip;
            end = start + JurikTrimCount - 1;
        }
        else
        {
            // for shorter history, use central ~50% as a reasonable proxy
            int slice = (int)Math.Max(5, Math.Round(count * 0.5));
            int drop = (count - slice) / 2;
            start = drop;
            end = drop + slice - 1;
        }

        if (start < 0) start = 0;
        if (end >= count) end = count - 1;

        int len = end - start + 1;
        return sorted.Slice(start, len).SumSIMD() / len;
    }
}