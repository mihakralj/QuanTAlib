using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DYMOI: Dynamic Momentum Index
/// </summary>
/// <remarks>
/// Volatility-adaptive RSI by Tushar Chande and Stanley Kroll (1994).
/// Three-stage pipeline:
/// 1. Dual circular-buffer StdDev → volatility ratio V = σ_short / σ_long
/// 2. dynamic_period = clamp(round(basePeriod / V), minPeriod, maxPeriod)
/// 3. Wilder RMA RSI with per-bar adaptive alpha = 1 / dynamic_period
///
/// When V > 1 (recent vol > long-term vol) the period shortens → faster RSI.
/// When V &lt; 1 (recent vol &lt; long-term vol) the period lengthens → smoother RSI.
///
/// References:
///   Chande, T. &amp; Kroll, S. (1994). The New Technical Trader.
///   PineScript reference: dymoi.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Dymoi : AbstractBase
{
    private readonly int _basePeriod;
    private readonly int _shortPeriod;
    private readonly int _longPeriod;
    private readonly int _minPeriod;
    private readonly int _maxPeriod;

    // Circular buffers for StdDev windows — heap objects, snapshotted separately
    private readonly double[] _shortBuf;
    private readonly double[] _longBuf;
    private readonly double[] _shortBufSnap;
    private readonly double[] _longBufSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        // StdDev running sums
        double SumShort,
        double SumSqShort,
        int HeadShort,
        int CountShort,
        double SumLong,
        double SumSqLong,
        int HeadLong,
        int CountLong,
        // Wilder RMA state
        double AvgGain,
        double AvgLoss,
        double E,         // warmup compensator: beta^n
        bool Warmup,
        double PrevClose,
        double LastValid);

    private State _s, _ps;

    /// <summary>
    /// Creates DYMOI with specified parameters.
    /// </summary>
    /// <param name="basePeriod">Base RSI period (must be &gt;= 2)</param>
    /// <param name="shortPeriod">Short StdDev window (must be &gt;= 2)</param>
    /// <param name="longPeriod">Long StdDev window (must be &gt;= 2 and &gt; shortPeriod)</param>
    /// <param name="minPeriod">Minimum dynamic period (must be &gt;= 2)</param>
    /// <param name="maxPeriod">Maximum dynamic period (must be &gt;= minPeriod)</param>
    public Dymoi(int basePeriod = 14, int shortPeriod = 5, int longPeriod = 10,
                 int minPeriod = 3, int maxPeriod = 30)
    {
        if (basePeriod < 2)
        {
            throw new ArgumentException("basePeriod must be >= 2", nameof(basePeriod));
        }

        if (shortPeriod < 2)
        {
            throw new ArgumentException("shortPeriod must be >= 2", nameof(shortPeriod));
        }

        if (longPeriod < 2 || longPeriod <= shortPeriod)
        {
            throw new ArgumentException("longPeriod must be >= 2 and > shortPeriod", nameof(longPeriod));
        }

        if (minPeriod < 2)
        {
            throw new ArgumentException("minPeriod must be >= 2", nameof(minPeriod));
        }

        if (maxPeriod < minPeriod)
        {
            throw new ArgumentException("maxPeriod must be >= minPeriod", nameof(maxPeriod));
        }

        _basePeriod = basePeriod;
        _shortPeriod = shortPeriod;
        _longPeriod = longPeriod;
        _minPeriod = minPeriod;
        _maxPeriod = maxPeriod;

        _shortBuf = new double[shortPeriod];
        _longBuf = new double[longPeriod];
        _shortBufSnap = new double[shortPeriod];
        _longBufSnap = new double[longPeriod];

        _s = new State(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1.0, true, double.NaN, double.NaN);
        _ps = _s;

        Name = $"Dymoi({basePeriod},{shortPeriod},{longPeriod},{minPeriod},{maxPeriod})";
        WarmupPeriod = longPeriod + maxPeriod;
    }

    /// <summary>
    /// Creates DYMOI with event-based source chaining.
    /// </summary>
    public Dymoi(ITValuePublisher source, int basePeriod = 14, int shortPeriod = 5,
                 int longPeriod = 10, int minPeriod = 3, int maxPeriod = 30)
        : this(basePeriod, shortPeriod, longPeriod, minPeriod, maxPeriod)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True once longPeriod + maxPeriod bars have been seen (worst-case warmup).
    /// </summary>
    public override bool IsHot => _s.CountLong >= _longPeriod && _s.CountShort >= _shortPeriod
                                  && !_s.Warmup;

    /// <summary>Base RSI period.</summary>
    public int BasePeriod => _basePeriod;

    /// <summary>Short StdDev window.</summary>
    public int ShortPeriod => _shortPeriod;

    /// <summary>Long StdDev window.</summary>
    public int LongPeriod => _longPeriod;

    /// <summary>Minimum allowable dynamic period.</summary>
    public int MinPeriod => _minPeriod;

    /// <summary>Maximum allowable dynamic period.</summary>
    public int MaxPeriod => _maxPeriod;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // Sanitize input
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(_s.LastValid) ? _s.LastValid : 0.0;
        }

        if (isNew)
        {
            _ps = _s;
            Array.Copy(_shortBuf, _shortBufSnap, _shortPeriod);
            Array.Copy(_longBuf, _longBufSnap, _longPeriod);
        }
        else
        {
            _s = _ps;
            Array.Copy(_shortBufSnap, _shortBuf, _shortPeriod);
            Array.Copy(_longBufSnap, _longBuf, _longPeriod);
        }

        var s = _s;

        // Update LastValid after rollback so we capture the sanitized value
        if (double.IsFinite(input.Value))
        {
            s.LastValid = value;
        }

        // ── Stage 1: StdDev short window (O(1) update) ──
        double oldestShort = _shortBuf[s.HeadShort];
        if (s.CountShort >= _shortPeriod)
        {
            s.SumShort -= oldestShort;
            s.SumSqShort -= oldestShort * oldestShort;
        }

        _shortBuf[s.HeadShort] = value;
        s.SumShort += value;
        s.SumSqShort += value * value;
        s.HeadShort = (s.HeadShort + 1) % _shortPeriod;
        if (s.CountShort < _shortPeriod)
        {
            s.CountShort++;
        }

        int nShort = s.CountShort;
        double meanShort = s.SumShort / nShort;
        double varShort = (s.SumSqShort / nShort) - (meanShort * meanShort);
        double sdShort = varShort > 0.0 ? Math.Sqrt(varShort) : 0.0;

        // ── Stage 1: StdDev long window (O(1) update) ──
        double oldestLong = _longBuf[s.HeadLong];
        if (s.CountLong >= _longPeriod)
        {
            s.SumLong -= oldestLong;
            s.SumSqLong -= oldestLong * oldestLong;
        }

        _longBuf[s.HeadLong] = value;
        s.SumLong += value;
        s.SumSqLong += value * value;
        s.HeadLong = (s.HeadLong + 1) % _longPeriod;
        if (s.CountLong < _longPeriod)
        {
            s.CountLong++;
        }

        int nLong = s.CountLong;
        double meanLong = s.SumLong / nLong;
        double varLong = (s.SumSqLong / nLong) - (meanLong * meanLong);
        double sdLong = varLong > 0.0 ? Math.Sqrt(varLong) : 0.0;

        // ── Stage 2: dynamic period ──
        double v = sdLong > 1e-10 ? sdShort / sdLong : 1.0;
        int dynPeriod;
        if (v > 1e-10)
        {
            double raw = _basePeriod / v;
            int rounded = (int)Math.Round(raw);
            dynPeriod = Math.Max(_minPeriod, Math.Min(_maxPeriod, rounded));
        }
        else
        {
            dynPeriod = _maxPeriod;
        }

        // ── Stage 3: Wilder RMA RSI with adaptive alpha ──
        double dymoi = 50.0;
        if (!double.IsNaN(s.PrevClose))
        {
            double alpha = 1.0 / dynPeriod;
            double beta = 1.0 - alpha;
            double change = value - s.PrevClose;
            double gain = change > 0.0 ? change : 0.0;
            double loss = change < 0.0 ? -change : 0.0;

            s.AvgGain = Math.FusedMultiplyAdd(s.AvgGain, beta, alpha * gain);
            s.AvgLoss = Math.FusedMultiplyAdd(s.AvgLoss, beta, alpha * loss);

            if (s.Warmup)
            {
                s.E *= beta;
                double c = s.E > 1e-10 ? 1.0 / (1.0 - s.E) : 1.0;
                double aG = s.AvgGain * c;
                double aL = s.AvgLoss * c;
                double total = aG + aL;
                dymoi = total != 0.0 ? 100.0 * aG / total : 50.0;
                if (s.E <= 1e-10)
                {
                    s.Warmup = false;
                }
            }
            else
            {
                double total = s.AvgGain + s.AvgLoss;
                dymoi = total != 0.0 ? 100.0 * s.AvgGain / total : 50.0;
            }
        }

        s.PrevClose = value;
        _s = s;

        dymoi = Math.Max(0.0, Math.Min(100.0, dymoi));
        Last = new TValue(input.Time, dymoi);
        PubEvent(Last, isNew);
        return Last;
    }
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _basePeriod, _shortPeriod, _longPeriod, _minPeriod, _maxPeriod);
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
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _s = new State(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1.0, true, double.NaN, double.NaN);
        _ps = _s;
        Array.Clear(_shortBuf, 0, _shortPeriod);
        Array.Clear(_longBuf, 0, _longPeriod);
        Array.Clear(_shortBufSnap, 0, _shortPeriod);
        Array.Clear(_longBufSnap, 0, _longPeriod);
        Last = default;
    }

    /// <summary>
    /// Batch static: TSeries → TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int basePeriod = 14, int shortPeriod = 5,
                                int longPeriod = 10, int minPeriod = 3, int maxPeriod = 30)
    {
        var dymoi = new Dymoi(basePeriod, shortPeriod, longPeriod, minPeriod, maxPeriod);
        return dymoi.Update(source);
    }

    /// <summary>
    /// Batch static: span → span.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int basePeriod = 14, int shortPeriod = 5, int longPeriod = 10,
        int minPeriod = 3, int maxPeriod = 30)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (basePeriod < 2)
        {
            throw new ArgumentException("basePeriod must be >= 2", nameof(basePeriod));
        }

        if (shortPeriod < 2)
        {
            throw new ArgumentException("shortPeriod must be >= 2", nameof(shortPeriod));
        }

        if (longPeriod < 2 || longPeriod <= shortPeriod)
        {
            throw new ArgumentException("longPeriod must be >= 2 and > shortPeriod", nameof(longPeriod));
        }

        if (minPeriod < 2)
        {
            throw new ArgumentException("minPeriod must be >= 2", nameof(minPeriod));
        }

        if (maxPeriod < minPeriod)
        {
            throw new ArgumentException("maxPeriod must be >= minPeriod", nameof(maxPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;

        double[]? rentedShort = null;
        double[]? rentedLong = null;
        scoped Span<double> shortBuf;
        scoped Span<double> longBuf;

        if (shortPeriod <= StackallocThreshold)
        {
            shortBuf = stackalloc double[shortPeriod];
        }
        else
        {
            rentedShort = System.Buffers.ArrayPool<double>.Shared.Rent(shortPeriod);
            shortBuf = rentedShort.AsSpan(0, shortPeriod);
        }

        if (longPeriod <= StackallocThreshold)
        {
            longBuf = stackalloc double[longPeriod];
        }
        else
        {
            rentedLong = System.Buffers.ArrayPool<double>.Shared.Rent(longPeriod);
            longBuf = rentedLong.AsSpan(0, longPeriod);
        }

        try
        {
            shortBuf.Clear();
            longBuf.Clear();

            double sumShort = 0, sumSqShort = 0;
            double sumLong = 0, sumSqLong = 0;
            int headShort = 0, countShort = 0;
            int headLong = 0, countLong = 0;

            double avgGain = 0, avgLoss = 0;
            double e = 1.0;
            bool warmup = true;
            double prevClose = double.NaN;
            double lastValid = double.NaN;

            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (!double.IsFinite(val))
                {
                    val = double.IsFinite(lastValid) ? lastValid : 0.0;
                }
                else
                {
                    lastValid = val;
                }

                // Short StdDev update
                double oldS = shortBuf[headShort];
                if (countShort >= shortPeriod)
                {
                    sumShort -= oldS;
                    sumSqShort -= oldS * oldS;
                }

                shortBuf[headShort] = val;
                sumShort += val;
                sumSqShort += val * val;
                headShort = (headShort + 1) % shortPeriod;
                if (countShort < shortPeriod)
                {
                    countShort++;
                }

                double meanS = sumShort / countShort;
                double varS = (sumSqShort / countShort) - (meanS * meanS);
                double sdShort = varS > 0.0 ? Math.Sqrt(varS) : 0.0;

                // Long StdDev update
                double oldL = longBuf[headLong];
                if (countLong >= longPeriod)
                {
                    sumLong -= oldL;
                    sumSqLong -= oldL * oldL;
                }

                longBuf[headLong] = val;
                sumLong += val;
                sumSqLong += val * val;
                headLong = (headLong + 1) % longPeriod;
                if (countLong < longPeriod)
                {
                    countLong++;
                }

                double meanL = sumLong / countLong;
                double varL = (sumSqLong / countLong) - (meanL * meanL);
                double sdLong = varL > 0.0 ? Math.Sqrt(varL) : 0.0;

                // Dynamic period
                double v = sdLong > 1e-10 ? sdShort / sdLong : 1.0;
                int dynPeriod;
                if (v > 1e-10)
                {
                    int rounded = (int)Math.Round(basePeriod / v);
                    dynPeriod = Math.Max(minPeriod, Math.Min(maxPeriod, rounded));
                }
                else
                {
                    dynPeriod = maxPeriod;
                }

                // Wilder RMA RSI
                double dymoi = 50.0;
                if (!double.IsNaN(prevClose))
                {
                    double alpha = 1.0 / dynPeriod;
                    double beta = 1.0 - alpha;
                    double change = val - prevClose;
                    double gain = change > 0.0 ? change : 0.0;
                    double loss = change < 0.0 ? -change : 0.0;

                    avgGain = Math.FusedMultiplyAdd(avgGain, beta, alpha * gain);
                    avgLoss = Math.FusedMultiplyAdd(avgLoss, beta, alpha * loss);

                    if (warmup)
                    {
                        e *= beta;
                        double c = e > 1e-10 ? 1.0 / (1.0 - e) : 1.0;
                        double aG = avgGain * c;
                        double aL = avgLoss * c;
                        double total = aG + aL;
                        dymoi = total != 0.0 ? 100.0 * aG / total : 50.0;
                        if (e <= 1e-10)
                        {
                            warmup = false;
                        }
                    }
                    else
                    {
                        double total = avgGain + avgLoss;
                        dymoi = total != 0.0 ? 100.0 * avgGain / total : 50.0;
                    }
                }

                prevClose = val;
                output[i] = Math.Max(0.0, Math.Min(100.0, dymoi));
            }
        }
        finally
        {
            if (rentedShort != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedShort);
            }

            if (rentedLong != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedLong);
            }
        }
    }
}
