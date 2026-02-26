// COPPOCK: Coppock Curve
// WMA of the sum of two Rate-of-Change values at different lookback periods.
// Formula: Coppock = WMA(ROC(longRoc) + ROC(shortRoc), wmaPeriod)
// Source: Edwin Coppock, "A Guide to the Use of Coppock Curve", Barron's (1962)

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// COPPOCK: Coppock Curve
/// </summary>
/// <remarks>
/// The Coppock Curve applies a Weighted Moving Average to the sum of two
/// Rate-of-Change calculations at different lookback periods, producing a
/// zero-centered oscillator. Zero-line crossovers from below signal long-term
/// buying opportunities on monthly charts.
///
/// Calculation:
/// 1. ROC_long  = (price / price[longRoc]  - 1) * 100
/// 2. ROC_short = (price / price[shortRoc] - 1) * 100
/// 3. Combined  = ROC_long + ROC_short
/// 4. Coppock   = WMA(Combined, wmaPeriod)
///
/// Default parameters: longRoc=14, shortRoc=11, wmaPeriod=10 (original monthly values)
/// WarmupPeriod = max(longRoc, shortRoc) + wmaPeriod - 1
///
/// Sources:
/// - Coppock, E.S.C. (1962). "A Guide to the Use of Coppock Curve." Barron's
/// - Kirkpatrick, C. &amp; Dahlquist, J. (2010). Technical Analysis, Chapter 15
/// </remarks>
[SkipLocalsInit]
public sealed class Coppock : ITValuePublisher
{
    private const int DefaultLongRoc = 14;
    private const int DefaultShortRoc = 11;
    private const int DefaultWmaPeriod = 10;

    private readonly int _longRoc;
    private readonly int _shortRoc;
    private readonly int _wmaPeriod;
    private readonly double _wmaNorm; // W*(W+1)/2

    // ROC lookback ring buffers: slot[head] = oldest price still needed
    // Size = period+1 so we can store current + lookback[period] simultaneously
    private readonly double[] _longBuf;   // size = longRoc+1
    private readonly double[] _shortBuf;  // size = shortRoc+1

    // WMA dual-running-sum ring buffer
    private readonly double[] _wmaBuf;    // size = wmaPeriod

    // All scalar state grouped for _ps = _s snapshot (bar-correction).
    // PrevLong / PrevShort / PrevWma: slot values BEFORE the last isNew=true write,
    // used to restore ring-buffer slots on isNew=false rollback.
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int LongHead, int ShortHead,
        double PrevLong, double PrevShort,
        int WmaHead, int WmaCount,
        double WmaPlainSum, double WmaWeightedSum,
        double PrevWma,
        int Count, double LastValidPrice);

    private State _s;
    private State _ps;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _s.Count >= WarmupPeriod;

    public event TValuePublishedHandler? Pub;

    public Coppock(int longRoc = DefaultLongRoc, int shortRoc = DefaultShortRoc, int wmaPeriod = DefaultWmaPeriod)
    {
        if (longRoc <= 0)
        {
            throw new ArgumentException("Long ROC period must be greater than 0", nameof(longRoc));
        }
        if (shortRoc <= 0)
        {
            throw new ArgumentException("Short ROC period must be greater than 0", nameof(shortRoc));
        }
        if (wmaPeriod <= 0)
        {
            throw new ArgumentException("WMA period must be greater than 0", nameof(wmaPeriod));
        }

        _longRoc = longRoc;
        _shortRoc = shortRoc;
        _wmaPeriod = wmaPeriod;
        _wmaNorm = wmaPeriod * (wmaPeriod + 1) * 0.5;

        _longBuf = new double[longRoc + 1];
        _shortBuf = new double[shortRoc + 1];
        _wmaBuf = new double[wmaPeriod];

        // Warmup: need max(longRoc,shortRoc) bars before combined ROC is non-zero,
        // then wmaPeriod bars to fill WMA window. Subtract 1 for the shared bar.
        WarmupPeriod = Math.Max(longRoc, shortRoc) + wmaPeriod - 1;

        _s = default;
        _ps = _s;
        Name = $"Coppock({longRoc},{shortRoc},{wmaPeriod})";
    }

    public Coppock(ITValuePublisher source, int longRoc = DefaultLongRoc, int shortRoc = DefaultShortRoc, int wmaPeriod = DefaultWmaPeriod)
        : this(longRoc, shortRoc, wmaPeriod)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            // Restore ring-buffer slots overwritten by the last isNew=true call.
            _longBuf[_ps.LongHead] = _s.PrevLong;
            _shortBuf[_ps.ShortHead] = _s.PrevShort;
            _wmaBuf[_ps.WmaHead] = _s.PrevWma;
            _s = _ps;
        }

        // Local copy for JIT register promotion
        int longH = _s.LongHead;
        int shortH = _s.ShortHead;
        int wmaH = _s.WmaHead;
        int wmaCount = _s.WmaCount;
        double plainSum = _s.WmaPlainSum;
        double weightedSum = _s.WmaWeightedSum;
        int count = _s.Count;
        double lastValid = _s.LastValidPrice;

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            price = double.IsFinite(lastValid) ? lastValid : 0.0;
        }
        else
        {
            lastValid = price;
        }

        if (isNew)
        {
            count++;
        }

        // ── ROC lookback ring buffers ─────────────────────────────────────────
        // Capture slot value BEFORE writing (for restore on next isNew=false).
        double prevLong = _longBuf[longH];
        double prevShort = _shortBuf[shortH];

        _longBuf[longH] = price;
        _shortBuf[shortH] = price;

        if (isNew)
        {
            longH = (longH + 1) % (_longRoc + 1);
            shortH = (shortH + 1) % (_shortRoc + 1);
        }

        // ── Combined ROC ──────────────────────────────────────────────────────
        double rocLong = prevLong != 0.0 ? 100.0 * (price - prevLong) / prevLong : 0.0;
        double rocShort = prevShort != 0.0 ? 100.0 * (price - prevShort) / prevShort : 0.0;
        double combined = rocLong + rocShort;

        // ── WMA dual running sum (O(1) per bar) ───────────────────────────────
        // When buffer is growing (wmaCount < wmaPeriod):
        //   plainSum    += combined
        //   weightedSum += (wmaCount+1) * combined   [1-based weight]
        // When buffer is full (wmaCount == wmaPeriod):
        //   oldest evicted from slot wmaH
        //   plainSum     = plainSum - oldest + combined
        //   weightedSum  = weightedSum - (plainSum_before_eviction) + wmaPeriod * combined
        double prevWma = _wmaBuf[wmaH];
        double coppockVal;

        if (wmaCount < _wmaPeriod)
        {
            plainSum += combined;
            wmaCount++;
            weightedSum += wmaCount * combined; // weight = position 1..wmaPeriod
            double norm = wmaCount * (wmaCount + 1) * 0.5;
            coppockVal = norm != 0.0 ? weightedSum / norm : 0.0;
        }
        else
        {
            double oldPlain = plainSum;
            plainSum = plainSum - prevWma + combined;
            weightedSum = weightedSum - oldPlain + _wmaPeriod * combined;
            coppockVal = weightedSum / _wmaNorm;
        }

        _wmaBuf[wmaH] = combined;
        if (isNew)
        {
            wmaH = (wmaH + 1) % _wmaPeriod;
        }

        // ── Write back state (including pre-write slot snapshots) ─────────────
        _s = new State(
            longH, shortH,
            prevLong, prevShort,
            wmaH, wmaCount,
            plainSum, weightedSum,
            prevWma,
            count, lastValid);

        Last = new TValue(input.Time, coppockVal);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>Updates streaming state from a <see cref="TSeries"/> and returns output series.</summary>
    public TSeries Update(TSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return new TSeries([], []);
        }

        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.Values, CollectionsMarshal.AsSpan(v), _longRoc, _shortRoc, _wmaPeriod);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        // Prime streaming state to match end of batch
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    /// <summary>Resets all internal state.</summary>
    public void Reset()
    {
        Array.Clear(_longBuf);
        Array.Clear(_shortBuf);
        Array.Clear(_wmaBuf);
        _s = default;
        _ps = _s;
        Last = default;
    }

    // ── Static Span Batch ────────────────────────────────────────────────────

    /// <summary>
    /// Calculates Coppock for the full source span. Uses <see cref="ArrayPool{T}"/> for all intermediate buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> output,
        int longRoc = DefaultLongRoc,
        int shortRoc = DefaultShortRoc,
        int wmaPeriod = DefaultWmaPeriod)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (longRoc <= 0)
        {
            throw new ArgumentException("Long ROC period must be greater than 0", nameof(longRoc));
        }
        if (shortRoc <= 0)
        {
            throw new ArgumentException("Short ROC period must be greater than 0", nameof(shortRoc));
        }
        if (wmaPeriod <= 0)
        {
            throw new ArgumentException("WMA period must be greater than 0", nameof(wmaPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        int lBufSize = longRoc + 1;
        int sBufSize = shortRoc + 1;
        double wmaNorm = wmaPeriod * (wmaPeriod + 1) * 0.5;

        double[] longBuf = ArrayPool<double>.Shared.Rent(lBufSize);
        double[] shortBuf = ArrayPool<double>.Shared.Rent(sBufSize);
        double[] wmaBuf = ArrayPool<double>.Shared.Rent(wmaPeriod);

        longBuf.AsSpan(0, lBufSize).Clear();
        shortBuf.AsSpan(0, sBufSize).Clear();
        wmaBuf.AsSpan(0, wmaPeriod).Clear();

        try
        {
            int longH = 0, shortH = 0, wmaH = 0, wmaCount = 0;
            double plainSum = 0.0, weightedSum = 0.0;
            double lastValid = 0.0;

            for (int i = 0; i < len; i++)
            {
                double price = source[i];
                if (!double.IsFinite(price))
                {
                    price = lastValid;
                }
                else
                {
                    lastValid = price;
                }

                double prevLong = longBuf[longH];
                double prevShort = shortBuf[shortH];
                longBuf[longH] = price;
                shortBuf[shortH] = price;
                longH = (longH + 1) % lBufSize;
                shortH = (shortH + 1) % sBufSize;

                double rocLong = prevLong != 0.0 ? 100.0 * (price - prevLong) / prevLong : 0.0;
                double rocShort = prevShort != 0.0 ? 100.0 * (price - prevShort) / prevShort : 0.0;
                double combined = rocLong + rocShort;

                double oldest = wmaBuf[wmaH];
                double coppockVal;
                if (wmaCount < wmaPeriod)
                {
                    plainSum += combined;
                    wmaCount++;
                    weightedSum += wmaCount * combined;
                    double norm = wmaCount * (wmaCount + 1) * 0.5;
                    coppockVal = norm != 0.0 ? weightedSum / norm : 0.0;
                }
                else
                {
                    double oldPlain = plainSum;
                    plainSum = plainSum - oldest + combined;
                    weightedSum = weightedSum - oldPlain + wmaPeriod * combined;
                    coppockVal = weightedSum / wmaNorm;
                }
                wmaBuf[wmaH] = combined;
                wmaH = (wmaH + 1) % wmaPeriod;

                output[i] = coppockVal;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(longBuf);
            ArrayPool<double>.Shared.Return(shortBuf);
            ArrayPool<double>.Shared.Return(wmaBuf);
        }
    }

    /// <summary>Calculates Coppock for an entire <see cref="TSeries"/>.</summary>
    public static TSeries Batch(
        TSeries source,
        int longRoc = DefaultLongRoc,
        int shortRoc = DefaultShortRoc,
        int wmaPeriod = DefaultWmaPeriod)
    {
        if (source == null || source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.Values, CollectionsMarshal.AsSpan(v), longRoc, shortRoc, wmaPeriod);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>Creates a Coppock indicator and calculates results for the source series.</summary>
    public static (TSeries Results, Coppock Indicator) Calculate(
        TSeries source,
        int longRoc = DefaultLongRoc,
        int shortRoc = DefaultShortRoc,
        int wmaPeriod = DefaultWmaPeriod)
    {
        var indicator = new Coppock(longRoc, shortRoc, wmaPeriod);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
