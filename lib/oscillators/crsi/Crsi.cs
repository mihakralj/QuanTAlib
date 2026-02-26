using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CRSI: Connors RSI
/// </summary>
/// <remarks>
/// Composite momentum oscillator combining three independent measurements:
/// 1. Price RSI (Wilder smoothing, default period 3)
/// 2. RSI of consecutive up/down streak length (default period 2)
/// 3. Percent rank of 1-bar ROC over a lookback window (default period 100)
///
/// CRSI = (PriceRSI + StreakRSI + PercentRank) / 3, clamped to [0, 100].
///
/// References:
///   Connors, L. &amp; Alvarez, C. (2012). An Introduction to ConnorsRSI. TradingMarkets.
///   PineScript reference: crsi.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Crsi : AbstractBase
{
    private readonly int _rsiPeriod;
    private readonly int _streakPeriod;
    private readonly int _rankPeriod;

    // Sub-indicators
    private readonly Rsi _priceRsi;
    private readonly Rsi _streakRsi;

    // Circular buffer for ROC percent-rank (stores close prices, size = rankPeriod + 1)
    // We store the last rankPeriod+1 closing prices so we can compute 1-bar ROC for each slot
    // and then do the percent-rank scan.
    // Actually: store the ROC values directly (rankPeriod slots).
    private readonly double[] _rocBuf;
    private readonly double[] _rocBufSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int Streak,
        double PrevClose,
        int RocHead,
        int RocCount,
        double PrevRocSlot,
        double LastValid);

    private State _s, _ps;

    /// <summary>
    /// Creates CRSI with specified periods.
    /// </summary>
    /// <param name="rsiPeriod">Price RSI period (must be &gt; 0)</param>
    /// <param name="streakPeriod">Streak RSI period (must be &gt; 0)</param>
    /// <param name="rankPeriod">Percent rank lookback period (must be &gt; 0)</param>
    public Crsi(int rsiPeriod = 3, int streakPeriod = 2, int rankPeriod = 100)
    {
        if (rsiPeriod <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(rsiPeriod));
        }

        if (streakPeriod <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(streakPeriod));
        }

        if (rankPeriod <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(rankPeriod));
        }

        _rsiPeriod = rsiPeriod;
        _streakPeriod = streakPeriod;
        _rankPeriod = rankPeriod;

        _priceRsi = new Rsi(rsiPeriod);
        _streakRsi = new Rsi(streakPeriod);

        _rocBuf = new double[rankPeriod];
        _rocBufSnap = new double[rankPeriod];

        _s = new State(0, double.NaN, 0, 0, double.NaN, double.NaN);
        _ps = _s;

        Name = $"Crsi({rsiPeriod},{streakPeriod},{rankPeriod})";
        WarmupPeriod = rankPeriod + rsiPeriod + 1;
    }

    /// <summary>
    /// Creates CRSI with event-based source chaining.
    /// </summary>
    public Crsi(ITValuePublisher source, int rsiPeriod = 3, int streakPeriod = 2, int rankPeriod = 100)
        : this(rsiPeriod, streakPeriod, rankPeriod)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True once the percent-rank buffer is full (dominant warmup component).
    /// </summary>
    public override bool IsHot => _s.RocCount >= _rankPeriod;

    /// <summary>
    /// Price RSI period.
    /// </summary>
    public int RsiPeriod => _rsiPeriod;

    /// <summary>
    /// Streak RSI period.
    /// </summary>
    public int StreakPeriod => _streakPeriod;

    /// <summary>
    /// Percent rank lookback period.
    /// </summary>
    public int RankPeriod => _rankPeriod;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // Sanitize input
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(_s.LastValid) ? _s.LastValid : 0.0;
        }
        else
        {
            _s.LastValid = value;
        }

        if (isNew)
        {
            // Snapshot state + ROC buffer before advancing
            _ps = _s;
            Array.Copy(_rocBuf, _rocBufSnap, _rankPeriod);
        }
        else
        {
            // Rollback: restore state and ROC buffer
            // Save the value in the slot we're about to restore (PrevRocSlot was set on last isNew=true)
            _s = _ps;
            Array.Copy(_rocBufSnap, _rocBuf, _rankPeriod);
        }

        var s = _s;

        // ── Component 1: Price RSI ──
        double priceRsiVal = _priceRsi.Update(new TValue(input.Time, value), isNew).Value;

        // ── Component 2: Streak ──
        int streak = s.Streak;
        if (!double.IsNaN(s.PrevClose))
        {
            if (value > s.PrevClose)
            {
                streak = streak >= 0 ? streak + 1 : 1;
            }
            else if (value < s.PrevClose)
            {
                streak = streak <= 0 ? streak - 1 : -1;
            }
            else
            {
                streak = 0;
            }
        }

        double streakRsiVal = _streakRsi.Update(new TValue(input.Time, (double)streak), isNew).Value;

        // ── Component 3: Percent rank of 1-bar ROC ──
        double roc = 0.0;
        if (!double.IsNaN(s.PrevClose) && s.PrevClose != 0.0)
        {
            roc = (value - s.PrevClose) / s.PrevClose * 100.0;
        }

        // Circular buffer: slot at RocHead holds the current (overwritten) ROC
        // PrevRocSlot saved the old value at RocHead before this bar wrote it (on isNew=true path)
        int head = s.RocHead;
        int count = s.RocCount;
        bool slotWasEmpty = (count < _rankPeriod);

        // Save old slot content (used by next rollback)
        s.PrevRocSlot = _rocBuf[head];

        _rocBuf[head] = roc;
        s.RocHead = (head + 1) % _rankPeriod;
        if (slotWasEmpty)
        {
            count++;
        }

        s.RocCount = count;

        // Percent rank: count how many entries in buffer are <= current roc
        int lessOrEqual = 0;
        for (int i = 0; i < count; i++)
        {
            if (_rocBuf[i] <= roc)
            {
                lessOrEqual++;
            }
        }

        double pctRank = count > 0 ? (double)lessOrEqual / count * 100.0 : 50.0;

        // Update prev close and streak in state
        s.PrevClose = value;
        s.Streak = streak;
        _s = s;

        // ── Compose ──
        double crsi = (priceRsiVal + streakRsiVal + pctRank) / 3.0;
        crsi = Math.Max(0.0, Math.Min(100.0, crsi));

        Last = new TValue(input.Time, crsi);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _rsiPeriod, _streakPeriod, _rankPeriod);
        source.Times.CopyTo(tSpan);

        // Rebuild streaming state to match end of series
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _priceRsi.Reset();
        _streakRsi.Reset();
        _s = new State(0, double.NaN, 0, 0, double.NaN, double.NaN);
        _ps = _s;
        Array.Clear(_rocBuf, 0, _rankPeriod);
        Array.Clear(_rocBufSnap, 0, _rankPeriod);
        Last = default;
    }

    /// <summary>
    /// Batch static: TSeries → TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int rsiPeriod = 3, int streakPeriod = 2, int rankPeriod = 100)
    {
        var crsi = new Crsi(rsiPeriod, streakPeriod, rankPeriod);
        return crsi.Update(source);
    }

    /// <summary>
    /// Batch static: span → span.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int rsiPeriod = 3, int streakPeriod = 2, int rankPeriod = 100)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (rsiPeriod <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(rsiPeriod));
        }

        if (streakPeriod <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(streakPeriod));
        }

        if (rankPeriod <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(rankPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Allocate streak series
        double[]? rentedStreak = null;
        scoped Span<double> streakBuf;
        const int StackallocThreshold = 256;
        if (len <= StackallocThreshold)
        {
            streakBuf = stackalloc double[len];
        }
        else
        {
            rentedStreak = System.Buffers.ArrayPool<double>.Shared.Rent(len);
            streakBuf = rentedStreak.AsSpan(0, len);
        }

        // Allocate ROC percent rank scratch (rankPeriod circular buffer)
        double[]? rentedRoc = null;
        scoped Span<double> rocBuf;
        if (rankPeriod <= StackallocThreshold)
        {
            rocBuf = stackalloc double[rankPeriod];
        }
        else
        {
            rentedRoc = System.Buffers.ArrayPool<double>.Shared.Rent(rankPeriod);
            rocBuf = rentedRoc.AsSpan(0, rankPeriod);
        }

        // Allocate priceRsi output and streakRsi output
        double[]? rentedPriceRsi = null;
        double[]? rentedStreakRsi = null;
        scoped Span<double> priceRsiOut;
        scoped Span<double> streakRsiOut;
        if (len <= StackallocThreshold)
        {
            priceRsiOut = stackalloc double[len];
            streakRsiOut = stackalloc double[len];
        }
        else
        {
            rentedPriceRsi = System.Buffers.ArrayPool<double>.Shared.Rent(len);
            rentedStreakRsi = System.Buffers.ArrayPool<double>.Shared.Rent(len);
            priceRsiOut = rentedPriceRsi.AsSpan(0, len);
            streakRsiOut = rentedStreakRsi.AsSpan(0, len);
        }

        try
        {
            // Compute streak values
            int streak = 0;
            double prevClose = double.NaN;
            for (int i = 0; i < len; i++)
            {
                double v = source[i];
                if (!double.IsFinite(v))
                {
                    v = double.IsFinite(prevClose) ? prevClose : 0.0;
                }

                if (!double.IsNaN(prevClose))
                {
                    if (v > prevClose)
                    {
                        streak = streak >= 0 ? streak + 1 : 1;
                    }
                    else if (v < prevClose)
                    {
                        streak = streak <= 0 ? streak - 1 : -1;
                    }
                    else
                    {
                        streak = 0;
                    }
                }

                streakBuf[i] = (double)streak;
                prevClose = v;
            }

            // Compute price RSI and streak RSI
            Rsi.Batch(source, priceRsiOut, rsiPeriod);
            Rsi.Batch(streakBuf, streakRsiOut, streakPeriod);

            // Compute percent rank of 1-bar ROC
            rocBuf.Clear();
            int rocHead = 0;
            int rocCount = 0;
            prevClose = double.NaN;

            for (int i = 0; i < len; i++)
            {
                double v = source[i];
                if (!double.IsFinite(v))
                {
                    v = double.IsFinite(prevClose) ? prevClose : 0.0;
                }

                double roc = 0.0;
                if (!double.IsNaN(prevClose) && prevClose != 0.0)
                {
                    roc = (v - prevClose) / prevClose * 100.0;
                }

                prevClose = v;

                bool wasEmpty = rocCount < rankPeriod;
                rocBuf[rocHead] = roc;
                rocHead = (rocHead + 1) % rankPeriod;
                if (wasEmpty)
                {
                    rocCount++;
                }

                int lessOrEqual = 0;
                for (int j = 0; j < rocCount; j++)
                {
                    if (rocBuf[j] <= roc)
                    {
                        lessOrEqual++;
                    }
                }

                double pctRank = rocCount > 0 ? (double)lessOrEqual / rocCount * 100.0 : 50.0;
                double crsi = (priceRsiOut[i] + streakRsiOut[i] + pctRank) / 3.0;
                output[i] = Math.Max(0.0, Math.Min(100.0, crsi));
            }
        }
        finally
        {
            if (rentedStreak != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedStreak);
            }

            if (rentedRoc != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedRoc);
            }

            if (rentedPriceRsi != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedPriceRsi);
            }

            if (rentedStreakRsi != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedStreakRsi);
            }
        }
    }
}
