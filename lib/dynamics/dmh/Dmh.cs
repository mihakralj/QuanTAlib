using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DMH: Ehlers Directional Movement with Hann Windowing
/// </summary>
/// <remarks>
/// Three-stage pipeline: DM extraction → EMA smoothing → Hann FIR filter.
/// Provides smoother directional movement with reduced lag via Hann windowing.
///
/// Calculation: <c>DMH = HannFIR(EMA(PlusDM - MinusDM))</c>
/// </remarks>
/// <seealso href="Dmh.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Dmh : ITValuePublisher
{
    private readonly int _period;
    private readonly double _sf;           // 1.0 / period (EMA smoothing factor)
    private readonly double[] _hannCoeffs; // Precomputed Hann window coefficients
    private readonly double _coefSum;      // Sum of all Hann coefficients
    private readonly RingBuffer _emaBuf;   // EMA history for FIR scan

    private TBar _prevBar;
    private TBar _lastInput;
    private bool _isInitialized;
    private double _ema;
    private int _count;

    // Snapshot state for bar correction
    private TBar _p_prevBar;
    private TBar _p_lastInput;
    private bool _p_isInitialized;
    private double _p_ema;
    private int _p_count;
    private RingBuffer.SnapshotToken _p_emaBufToken;

    public string Name { get; }
    public event TValuePublishedHandler? Pub;
    public TValue Last { get; private set; }
    public int WarmupPeriod { get; }

    /// <summary>
    /// True when the indicator has enough data for valid calculations.
    /// </summary>
    public bool IsHot => _count >= WarmupPeriod;

    public Dmh(int period = 14)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        _period = period;
        _sf = 1.0 / period;
        Name = $"Dmh({period})";
        WarmupPeriod = period + 1;

        // Precompute Hann window coefficients: w(k) = 1 - cos(2π·k / (period + 1))
        _hannCoeffs = new double[period];
        double sum = 0;
        double denom = period + 1.0;
        for (int k = 0; k < period; k++)
        {
            double w = 1.0 - Math.Cos(2.0 * Math.PI * (k + 1) / denom);
            _hannCoeffs[k] = w;
            sum += w;
        }
        _coefSum = sum;

        _emaBuf = new RingBuffer(period);
        _isInitialized = false;
        _ema = 0;
        _count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _prevBar = default;
        _lastInput = default;
        _isInitialized = false;
        _ema = 0;
        _count = 0;
        _emaBuf.Clear();
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            // Snapshot state BEFORE mutations
            _p_prevBar = _prevBar;
            _p_lastInput = _lastInput;
            _p_isInitialized = _isInitialized;
            _p_ema = _ema;
            _p_count = _count;
            _p_emaBufToken = _emaBuf.GetSnapshot();

            if (_isInitialized)
            {
                _prevBar = _lastInput;
            }
            else
            {
                _isInitialized = true;
            }

            _count++;
        }
        else
        {
            // Restore state from snapshot
            _prevBar = _p_prevBar;
            _lastInput = _p_lastInput;
            _isInitialized = _p_isInitialized;
            _ema = _p_ema;
            _count = _p_count;
            _emaBuf.RestoreSnapshot(_p_emaBufToken);

            if (_isInitialized)
            {
                _prevBar = _lastInput;
            }

            _count++;
        }

        _lastInput = input;

        // Stage 1: Classic DM extraction
        double plusDM = 0;
        double minusDM = 0;

        if (_prevBar.Time != 0)
        {
            double upMove = input.High - _prevBar.High;
            double downMove = _prevBar.Low - input.Low;

            if (upMove > downMove && upMove > 0)
            {
                plusDM = upMove;
            }

            if (downMove > upMove && downMove > 0)
            {
                minusDM = downMove;
            }
        }

        // Stage 2: EMA smoothing
        // EMA = SF * (PlusDM - MinusDM) + (1 - SF) * EMA
        double dmDiff = plusDM - minusDM;
        _ema = Math.FusedMultiplyAdd(_sf, dmDiff - _ema, _ema);

        // Add EMA to ring buffer
        _emaBuf.Add(_ema);

        // Stage 3: Hann FIR filter on EMA history
        double dmhValue = ComputeHannFir();

        // NaN/Infinity guard
        if (!double.IsFinite(dmhValue))
        {
            dmhValue = Last.Value;
        }

        Last = new TValue(input.Time, dmhValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    public TSeries Update(TBarSeries source)
    {
        int count = source.Count;
        if (count == 0)
        {
            return [];
        }

        var t = new List<long>(count);
        var v = new List<double>(count);
        CollectionsMarshal.SetCount(t, count);
        CollectionsMarshal.SetCount(v, count);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.High.Values, source.Low.Values, _period, vSpan);
        source.Close.Times.CopyTo(tSpan);

        // Restore streaming state by replaying tail bars
        Reset();
        int replayStart = Math.Max(0, count - (2 * _period));
        for (int i = replayStart; i < count; i++)
        {
            Update(source[i], isNew: true);
        }

        Last = new TValue(tSpan[count - 1], vSpan[count - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    public void Prime(TBarSeries source)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeHannFir()
    {
        int available = _emaBuf.Count;
        if (available == 0 || _coefSum == 0)
        {
            return 0;
        }

        // Hann FIR: Σ(w(k) * EMA[k-1]) / Σ(w(k)) for k=1..period
        // EMA[count-1] in Ehlers notation: count=1 → newest, count=2 → one ago, etc.
        // RingBuffer: index [Count-1] = newest, [Count-2] = one ago, etc.
        double dmSum = 0;
        double coefUsed = 0;
        int scanLen = Math.Min(_period, available);

        for (int k = 0; k < scanLen; k++)
        {
            double w = _hannCoeffs[k];
            // k=0 → EMA[0] (Ehlers count=1 → current EMA) → buffer newest → index [Count-1-k]
            double emaVal = _emaBuf[^(k + 1)];
            dmSum = Math.FusedMultiplyAdd(w, emaVal, dmSum);
            coefUsed += w;
        }

        return coefUsed > 0 ? dmSum / coefUsed : 0;
    }

    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
                             int period, Span<double> destination)
    {
        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        if (low.Length != len || destination.Length != len)
        {
            throw new ArgumentException("All input spans must have the same length.", nameof(destination));
        }

        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        double sf = 1.0 / period;

        // Precompute Hann coefficients
        const int StackallocThreshold = 64;
        double[]? hannRented = null;
        scoped Span<double> hannCoeffs;

        if (period <= StackallocThreshold)
        {
            hannCoeffs = stackalloc double[period];
        }
        else
        {
            hannRented = ArrayPool<double>.Shared.Rent(period);
            hannCoeffs = hannRented.AsSpan(0, period);
        }

        double coefSum = 0;
        double denom = period + 1.0;
        for (int k = 0; k < period; k++)
        {
            double w = 1.0 - Math.Cos(2.0 * Math.PI * (k + 1) / denom);
            hannCoeffs[k] = w;
            coefSum += w;
        }

        // EMA buffer for FIR scan
        double[]? emaRented = null;
        scoped Span<double> emaBuf;

        if (len <= StackallocThreshold)
        {
            emaBuf = stackalloc double[len];
        }
        else
        {
            emaRented = ArrayPool<double>.Shared.Rent(len);
            emaBuf = emaRented.AsSpan(0, len);
        }

        try
        {
            // Stage 1 + 2: DM extraction + EMA smoothing
            double ema = 0;
            emaBuf[0] = 0; // First bar: no previous bar, DM=0, EMA=0

            for (int i = 1; i < len; i++)
            {
                double upMove = high[i] - high[i - 1];
                double downMove = low[i - 1] - low[i];

                double plusDM = 0;
                double minusDM = 0;

                if (upMove > downMove && upMove > 0)
                {
                    plusDM = upMove;
                }

                if (downMove > upMove && downMove > 0)
                {
                    minusDM = downMove;
                }

                double dmDiff = plusDM - minusDM;
                ema = Math.FusedMultiplyAdd(sf, dmDiff - ema, ema);
                emaBuf[i] = ema;
            }

            // Stage 3: Hann FIR filter
            for (int i = 0; i < len; i++)
            {
                double dmSum = 0;
                double coefUsed = 0;
                int scanLen = Math.Min(period, i + 1);

                for (int k = 0; k < scanLen; k++)
                {
                    double w = hannCoeffs[k];
                    dmSum = Math.FusedMultiplyAdd(w, emaBuf[i - k], dmSum);
                    coefUsed += w;
                }

                destination[i] = coefUsed > 0 ? dmSum / coefUsed : 0;
            }
        }
        finally
        {
            if (hannRented != null)
            {
                ArrayPool<double>.Shared.Return(hannRented);
            }
            if (emaRented != null)
            {
                ArrayPool<double>.Shared.Return(emaRented);
            }
        }
    }

    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        var dmh = new Dmh(period);
        return dmh.Update(source);
    }

    public static (TSeries Results, Dmh Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Dmh(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
