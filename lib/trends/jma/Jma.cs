using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Jurik Moving Average (JMA):
/// - 10-bar SMA of local deviation
/// - 128-sample volatility distribution
/// - middle-65 trimmed mean as volatility reference
/// - Jurik dynamic exponent and 2-pole IIR core
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
    private readonly double _power;          // Jurik power parameter

    // Constants for trimmed mean
    private const int JurikTrimCount = 65; // canonical JMA: middle 65 of 128 samples

    // Buffers
    private readonly RingBuffer _devBuffer;
    private readonly RingBuffer _volBuffer;
    private readonly double[] _sorted;
    private readonly TValuePublishedHandler _handler;

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

        // --- Phase parameter: maps -100..100 -> 0.5..2.5 (Jurik convention) ---
        if (phase < -100)
            _phaseParam = 0.5;
        else if (phase > 100)
            _phaseParam = 2.5;
        else
            _phaseParam = (phase * 0.01) + 1.5;

        _power = power;

        // --- Length / log / divider parameters (from decompiled JMA) ---
        // L_raw ~ (period - 1)/2, with a tiny lower bound to avoid log(0)
        double lengthParam = period < 1.0000000002
            ? 0.0000000001
            : (period - 1.0) / 2.0;

        double logParam = Math.Log(Math.Sqrt(lengthParam)) / Math.Log(2.0);
        logParam = (logParam + 2.0) < 0.0 ? 0.0 : (logParam + 2.0);
        _logParam = logParam;

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
        Name = $"Jma({period},{phase},{power})"; // power kept for signature compatibility

        _devBuffer = new RingBuffer(DevWindowSize);
        _volBuffer = new RingBuffer(VolWindowSize);
        _sorted = new double[VolWindowSize];

        Reset();
    }

    public Jma(ITValuePublisher source, int period, int phase = 0, double power = 0.45)
        : this(period, phase, power)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _state = default;
        _p_state = default;
        _devBuffer.Clear();
        _volBuffer.Clear();
        Array.Clear(_sorted, 0, _sorted.Length);
        Last = default;
    }

    /// <summary>
    /// Core streaming step: feed a single value, get JMA.
    /// Honors isNew semantics by snapshotting state+buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Step(double value, bool isNew)
    {
        // --- Snapshot/rollback support for "amending" last bar ---
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

        // --- Handle NaN/inf: reuse last finite price ---
        if (!double.IsFinite(value))
        {
            if (_state.Bars == 0)
            {
                return double.NaN;
            }
            value = _state.LastPrice;
        }
        else
        {
            _state.LastPrice = value;
        }

        _state.Bars++;

        // --- First bar: initialize anchors and IIR state ---
        if (_state.Bars == 1)
        {
            _state.UpperBand = value;
            _state.LowerBand = value;
            _state.LastC0 = value;
            _state.LastC8 = 0.0;
            _state.LastA8 = 0.0;
            _state.LastJma = value;
            return value;
        }

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

        if (refVolatility <= 0.0)
            refVolatility = deviation;

        // 4. Jurik dynamic exponent d from abs/refVolatility
        // d = clamp( (abs/refVolatility)^p, 1 .. logParam )
        double ratio = absValue / refVolatility;
        if (ratio < 0.0) ratio = 0.0;

        double d = Math.Pow(ratio, _power);
        if (d > _logParam) d = _logParam;
        if (d < 1.0) d = 1.0;

        // 5. Update UpperBand / LowerBand using sqrtDivider ^ sqrt(d)
        // Optimization: Use Exp(log(x) * y) instead of Pow(x, y)
        double adapt = Math.Exp(_logSqrtDivider * Math.Sqrt(d));

        _state.UpperBand = (value > _state.UpperBand) ? value : value - (value - _state.UpperBand) * adapt;
        _state.LowerBand = (value < _state.LowerBand) ? value : value - (value - _state.LowerBand) * adapt;

        // 6. 2-pole IIR core using d as the "speed"
        // alpha = lengthDivider ^ d
        // matches the Jurik decompiled structure (fC0/fC8/fA8)
        double prevJma = _state.LastJma;
        if (double.IsNaN(prevJma) || _state.Bars == 2)
            prevJma = value;

        double alpha = Math.Exp(_logLengthDivider * d);
        double alpha2 = alpha * alpha;

        double c0 = (1.0 - alpha) * value + alpha * _state.LastC0;
        double c8 = (value - c0) * (1.0 - _lengthDivider) + _lengthDivider * _state.LastC8;
        double a8 = (_phaseParam * c8 + c0 - prevJma) *
                    (alpha * (-2.0) + alpha2 + 1.0) +
                    alpha2 * _state.LastA8;

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

        // Use static Calculate for performance
        // But JMA has complex parameters, so we need to pass them.
        // We can use the instance to calculate, but we need to be careful about state.
        // Or we can just loop using Step, which is what the original code did.
        // Since JMA is complex and not easily vectorizable, looping is fine.
        // But we should restore state afterwards.

        // RingBuffers are reference types, so we need to clone them or replay.
        // Replaying is safer and cleaner for complex state.

        Reset();
        for (int i = 0; i < len; i++)
        {
            double j = Step(source.Values[i], true);
            vSpan[i] = j;
        }

        // Restore state by replaying history
        // JMA needs a lot of history (128 bars for volatility).
        Reset();
        int lookback = Math.Max(VolWindowSize + 10, WarmupPeriod + 10);
        int startIndex = Math.Max(0, len - lookback);
        for (int i = startIndex; i < len; i++)
        {
            Step(source.Values[i], true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

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
        if (output.Length < source.Length)
            throw new ArgumentException("output span is shorter than source span.", nameof(output));

        var jma = new Jma(period, phase, power);
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = jma.Step(source[i], true);
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

        // Copy current buffer to _sorted for sorting
        _volBuffer.CopyTo(_sorted, 0);
        Array.Sort(_sorted, 0, count);

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
        return ((ReadOnlySpan<double>)_sorted.AsSpan(start, len)).SumSIMD() / len;
    }
}
