using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// JMA: Jurik Moving Average
/// </summary>
/// <remarks>
/// Proprietary adaptive filter with minimal lag and overshoot using volatility-based smoothing.
/// Combines 2-pole IIR core with trimmed-mean volatility estimation.
///
/// Key features: phase control [-100,100], adaptive band tracking, dynamic exponent.
///
/// The 128-sample volatility window uses Index-Linked Sorted Arrays (ILSA): a chronological
/// circular buffer plus two integer rank/position maps kept permanently sorted, so each bar
/// costs one 7-step binary search and a single memmove of 4-byte indices instead of a full
/// re-sort. The middle-65 trimmed mean is maintained as a running sum updated by deterministic
/// boundary-crossing deltas rather than 65 sequential additions.
/// </remarks>
/// <seealso href="Jma.md">Detailed documentation</seealso>
/// <seealso href="jma.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Jma : AbstractBase
{
    private const int VolWindowSize = 128; // volatility history length
    private const int VolIndexMask = VolWindowSize - 1;
    private const int DevWindowSize = 10;  // short SMA length for deviation

    private const int JurikTrimCount = 65;                   // canonical JMA: middle 65 of 128 samples
    private const int CoreLo = 32;                           // ceil((128-65)/2)
    private const int CoreHi = CoreLo + JurikTrimCount - 1;  // 96
    private const int MinVolSamples = 16;                    // below this, volatility is passed through

    // Periodic exact re-sum to bound floating-point drift of the incremental core sum.
    private const int SumRefreshInterval = 1024;

    // Jurik core parameters derived from period/phase
    private readonly double _phaseParam;     // 0.5 .. 2.5
    private readonly double _logParam;       // log(sqrt(L))/log(2) + 2, clamped >= 0
    private readonly double _lengthDivider;  // L'/(L'+2), L' = 0.9*L
    private readonly double _logSqrtDivider; // Precomputed log(_sqrtDivider) for Exp optimization
    private readonly double _logLengthDivider; // Precomputed log(_lengthDivider) for Exp optimization
    private readonly double _pExponent;      // max(logParam - 2, 0.5)

    private readonly RingBuffer _devBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    // --- ILSA: three contiguous 128-element arrays (1,536 bytes, L1-resident) ---
    private readonly double[] _chrono;   // C: chronological circular buffer of volatility values
    private readonly int[] _sortedToC;   // S_to_C: sorted rank -> chronological position
    private readonly int[] _cToSorted;   // C_to_S: chronological position -> sorted rank

    private int _volCount;
    private int _oldestC;   // chronological position of the expiring element (valid once full)
    private double _coreSum;
    private int _sumTicks;

    // --- Undo journal: restores the ILSA to the pre-update state for isNew=false replays ---
    private bool _undoValid;
    private bool _undoWasAppend;
    private int _undoOldC, _undoOldS, _undoNewS, _undoOldestC, _undoCount, _undoSumTicks;
    private double _undoOldVal, _undoCoreSum;

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

    public Jma(int period, int phase = 0)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        // --- Phase parameter: maps -100..100 -> 0.5..2.5 (Jurik convention) ---
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
        WarmupPeriod = (int)Math.Ceiling(20.0 + (80.0 * Math.Pow(period, 0.36)));

        _handler = Handle;
        Name = $"Jma({period},{phase})";

        _devBuffer = new RingBuffer(DevWindowSize);
        _chrono = GC.AllocateArray<double>(VolWindowSize, pinned: true);
        _sortedToC = GC.AllocateArray<int>(VolWindowSize, pinned: true);
        _cToSorted = GC.AllocateArray<int>(VolWindowSize, pinned: true);

        Reset();
    }

    public Jma(ITValuePublisher source, int period, int phase = 0)
        : this(period, phase)
    {
        _source = source;
        source.Pub += _handler;
    }

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        _devBuffer.Clear();
        _volCount = 0;
        _oldestC = 0;
        _coreSum = 0.0;
        _sumTicks = 0;
        _undoValid = false;
        Last = default;
    }

    #region ILSA volatility window

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SortedAt(int rank) => _chrono[_sortedToC[rank]];

    /// <summary>Value at <paramref name="rank"/> of the conceptual 127-element array with rank <paramref name="holeS"/> removed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GapAt(int rank, int holeS) => SortedAt(rank < holeS ? rank : rank + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResyncReverseMap(int fromRank, int toRank)
    {
        for (int i = fromRank; i <= toRank; i++)
        {
            _cToSorted[_sortedToC[i]] = i;
        }
    }

    /// <summary>First rank in [0,count) whose value exceeds <paramref name="value"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int UpperBound(double value, int count)
    {
        int lo = 0;
        int hi = count;
        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (SortedAt(mid) > value)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }
        return lo;
    }

    /// <summary>Binary search over the 127-element array that logically skips rank <paramref name="holeS"/>. Exactly 7 iterations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int UpperBoundWithHole(double value, int holeS)
    {
        int lo = 0;
        int hi = VolWindowSize - 1;
        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (GapAt(mid, holeS) > value)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }
        return lo;
    }

    private double RecomputeCoreSum()
    {
        double sum = 0.0;
        for (int i = CoreLo; i <= CoreHi; i++)
        {
            sum += SortedAt(i);
        }
        return sum;
    }

    /// <summary>Pushes one volatility sample and returns the reference volatility (trimmed mean).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double PushVolatility(double value)
        => _volCount < VolWindowSize ? AppendVolatility(value) : ReplaceOldestVolatility(value);

    private double AppendVolatility(double value)
    {
        int n = _volCount;
        int newS = UpperBound(value, n);

        _undoWasAppend = true;
        _undoNewS = newS;
        _undoCount = n;
        _undoOldestC = _oldestC;
        _undoCoreSum = _coreSum;
        _undoSumTicks = _sumTicks;
        _undoValid = true;

        if (n > newS)
        {
            Array.Copy(_sortedToC, newS, _sortedToC, newS + 1, n - newS);
        }
        _sortedToC[newS] = n;
        _chrono[n] = value;
        _volCount = n + 1;
        ResyncReverseMap(newS, n);

        if (_volCount == VolWindowSize)
        {
            _oldestC = 0;
            _sumTicks = 0;
            _coreSum = RecomputeCoreSum();
            return _coreSum / JurikTrimCount;
        }

        return _volCount < MinVolSamples ? value : WarmupTrimmedMean(_volCount);
    }

    private double ReplaceOldestVolatility(double value)
    {
        int oldC = _oldestC;
        int oldS = _cToSorted[oldC];
        double oldVal = _chrono[oldC];

        // Deletion delta, evaluated against the current 128-element sorted array.
        double deltaRemove = 0.0;
        if (oldS <= CoreHi)
        {
            double displaced = oldS < CoreLo ? SortedAt(CoreLo) : oldVal;
            deltaRemove = SortedAt(CoreHi + 1) - displaced;
        }

        // Insertion rank, and insertion delta against the 127-element post-deletion array.
        int newS = UpperBoundWithHole(value, oldS);
        double deltaInsert = 0.0;
        if (newS <= CoreHi)
        {
            double entering = newS < CoreLo ? GapAt(CoreLo - 1, oldS) : value;
            deltaInsert = entering - GapAt(CoreHi, oldS);
        }

        _undoWasAppend = false;
        _undoOldC = oldC;
        _undoOldS = oldS;
        _undoNewS = newS;
        _undoOldVal = oldVal;
        _undoOldestC = oldC;
        _undoCount = _volCount;
        _undoCoreSum = _coreSum;
        _undoSumTicks = _sumTicks;
        _undoValid = true;

        // Single contiguous shift of 4-byte indices closes the deletion gap and opens the insertion slot.
        if (newS <= oldS)
        {
            if (oldS > newS)
            {
                Array.Copy(_sortedToC, newS, _sortedToC, newS + 1, oldS - newS);
            }
            _sortedToC[newS] = oldC;
            ResyncReverseMap(newS, oldS);
        }
        else
        {
            Array.Copy(_sortedToC, oldS + 1, _sortedToC, oldS, newS - oldS);
            _sortedToC[newS] = oldC;
            ResyncReverseMap(oldS, newS);
        }

        _chrono[oldC] = value;
        _oldestC = (oldC + 1) & VolIndexMask;

        _coreSum += deltaRemove + deltaInsert;
        if (++_sumTicks >= SumRefreshInterval)
        {
            _sumTicks = 0;
            _coreSum = RecomputeCoreSum();
        }

        return _coreSum / JurikTrimCount;
    }

    /// <summary>Dynamic trim boundaries used while the 128-sample window is still filling.</summary>
    private double WarmupTrimmedMean(int count)
    {
        int slice = (int)Math.Max(5, Math.Round(count * 0.5));
        int drop = (count - slice) / 2;
        int start = drop < 0 ? 0 : drop;
        int end = drop + slice - 1;
        if (end >= count)
        {
            end = count - 1;
        }

        double sum = 0.0;
        for (int i = start; i <= end; i++)
        {
            sum += SortedAt(i);
        }
        return sum / (end - start + 1);
    }

    /// <summary>Reverses the most recent ILSA mutation (intrabar replay support).</summary>
    private void UndoVolatility()
    {
        if (!_undoValid)
        {
            return;
        }

        if (_undoWasAppend)
        {
            int n = _undoCount;
            int newS = _undoNewS;
            if (n > newS)
            {
                Array.Copy(_sortedToC, newS + 1, _sortedToC, newS, n - newS);
            }
            _volCount = n;
            ResyncReverseMap(newS, n - 1);
        }
        else
        {
            int oldS = _undoOldS;
            int newS = _undoNewS;
            int oldC = _undoOldC;
            if (newS <= oldS)
            {
                if (oldS > newS)
                {
                    Array.Copy(_sortedToC, newS + 1, _sortedToC, newS, oldS - newS);
                }
                _sortedToC[oldS] = oldC;
                ResyncReverseMap(newS, oldS);
            }
            else
            {
                Array.Copy(_sortedToC, oldS, _sortedToC, oldS + 1, newS - oldS);
                _sortedToC[oldS] = oldC;
                ResyncReverseMap(oldS, newS);
            }
            _chrono[oldC] = _undoOldVal;
        }

        _oldestC = _undoOldestC;
        _coreSum = _undoCoreSum;
        _sumTicks = _undoSumTicks;
        _undoValid = false;
    }

    #endregion

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
        if (_state.Bars == 1)
        {
            return InitializeFirstBar(value);
        }

        return CalculateJma(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleStateSnapshot(bool isNew)
    {
        if (isNew)
        {
            _p_state = _state;
            _devBuffer.Snapshot();
            _undoValid = false;
        }
        else
        {
            _state = _p_state;
            _devBuffer.Restore();
            UndoVolatility();
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
        double uBand = value - _state.UpperBand;
        double lBand = value - _state.LowerBand;
        double absUBand = Math.Abs(uBand);
        double absLBand = Math.Abs(lBand);
        double absValue = absUBand > absLBand ? absUBand : absLBand;
        double deviation = absValue + 1e-10;

        // 2. 10-bar SMA of local deviation -> "volatility"
        _devBuffer.Add(deviation);
        double volatility = _devBuffer.Average;

        // 3. 128-bar volatility history + middle-65 trimmed mean
        double refVolatility = PushVolatility(volatility);
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
        if (source.Count == 0)
        {
            return [];
        }

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
        _undoValid = false;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
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

    public static TSeries Batch(TSeries source, int period, int phase = 0)
    {
        var jma = new Jma(period, phase);
        return jma.Update(source);
    }

    /// <summary>
    /// Static helper compatible with your existing signature.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source,
                                 Span<double> output,
                                 int period,
                                 int phase = 0)
    {
        if (output.Length != source.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        var jma = new Jma(period, phase);
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = jma.Step(source[i], isNew: true);
        }
    }

    public static (TSeries Results, Jma Indicator) Calculate(TSeries source, int period, int phase = 0)
    {
        var indicator = new Jma(period, phase);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
