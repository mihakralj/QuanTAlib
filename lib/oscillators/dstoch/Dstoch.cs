using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DSTOCH: Double Stochastic (Bressert DSS).
/// Applies the Stochastic formula twice with EMA smoothing between stages.
/// Stage 1: rawK = 100 * (close - LL) / (HH - LL)  →  smoothK = EMA(rawK, period)
/// Stage 2: dsRaw = 100 * (smoothK - min(smoothK)) / (max(smoothK) - min(smoothK))  →  output = EMA(dsRaw, period)
/// Bounded [0, 100]. Uses MonotonicDeque for O(1) amortized min/max in both stages.
/// </summary>
[SkipLocalsInit]
public sealed class Dstoch : ITValuePublisher
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _decay;

    // Stage 1: HLC stochastic
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    // Stage 2: smoothK stochastic
    private readonly double[] _skBuf;
    private readonly MonotonicDeque _skMaxDeque;
    private readonly MonotonicDeque _skMinDeque;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SmK, double Dss,
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _count >= _period;

    public event TValuePublishedHandler? Pub;

    public Dstoch(int period = 21)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;

        _hBuf = new double[_period];
        _lBuf = new double[_period];
        _maxDeque = new MonotonicDeque(_period);
        _minDeque = new MonotonicDeque(_period);

        _skBuf = new double[_period];
        _skMaxDeque = new MonotonicDeque(_period);
        _skMinDeque = new MonotonicDeque(_period);

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"Dstoch({period})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    public Dstoch(TBarSeries source, int period = 21) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _index++;
            if (_count < _period)
            {
                _count++;
            }
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Validate inputs — substitute last-valid on NaN/Infinity
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        if (double.IsFinite(close)) { s.LastValidClose = close; }
        else { close = s.LastValidClose; }

        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Stage 1: Raw stochastic %K
        int bufIdx = _index < 0 ? 0 : (int)(_index % _period);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        if (isNew)
        {
            _maxDeque.PushMax(_index, high, _hBuf);
            _minDeque.PushMin(_index, low, _lBuf);
        }
        else
        {
            _maxDeque.RebuildMax(_hBuf, _index, _count);
            _minDeque.RebuildMin(_lBuf, _index, _count);
        }

        double highest = _maxDeque.GetExtremum(_hBuf);
        double lowest = _minDeque.GetExtremum(_lBuf);
        double range1 = highest - lowest;
        double rawK = range1 > 0.0 ? 100.0 * (close - lowest) / range1 : 0.0;

        // Stage 1 EMA: smooth rawK
        double smoothK = double.IsNaN(s.SmK)
            ? rawK
            : Math.FusedMultiplyAdd(s.SmK, _decay, _alpha * rawK);
        s.SmK = smoothK;

        // Stage 2: Stochastic of smoothK
        _skBuf[bufIdx] = smoothK;

        if (isNew)
        {
            _skMaxDeque.PushMax(_index, smoothK, _skBuf);
            _skMinDeque.PushMin(_index, smoothK, _skBuf);
        }
        else
        {
            _skMaxDeque.RebuildMax(_skBuf, _index, _count);
            _skMinDeque.RebuildMin(_skBuf, _index, _count);
        }

        double skMax = _skMaxDeque.GetExtremum(_skBuf);
        double skMin = _skMinDeque.GetExtremum(_skBuf);
        double range2 = skMax - skMin;
        double dsRaw = range2 > 0.0 ? 100.0 * (smoothK - skMin) / range2 : 0.0;

        // Stage 2 EMA: smooth dsRaw
        double dss = double.IsNaN(s.Dss)
            ? dsRaw
            : Math.FusedMultiplyAdd(s.Dss, _decay, _alpha * dsRaw);
        s.Dss = dss;

        _s = s;

        Last = new TValue(input.Time, dss);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var times = new List<long>(len);
        var vals = new List<double>(len);

        CollectionsMarshal.SetCount(times, len);
        CollectionsMarshal.SetCount(vals, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(vals), _period);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(times));

        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, CollectionsMarshal.AsSpan(vals)[^1]);

        return new TSeries(times, vals);
    }

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

    public void Reset()
    {
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        Array.Clear(_skBuf);
        _maxDeque.Reset();
        _minDeque.Reset();
        _skMaxDeque.Reset();
        _skMinDeque.Reset();
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 21)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length", nameof(high));
        }
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input", nameof(output));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;

        // Temporary buffers for Highest/Lowest results
        double[]? rentedUpper = null;
        double[]? rentedLower = null;
        double[]? rentedRawK = null;
        double[]? rentedSmK = null;
        double[]? rentedSmkUpper = null;
        double[]? rentedSmkLower = null;

        scoped Span<double> upperBuf;
        scoped Span<double> lowerBuf;
        scoped Span<double> rawKBuf;
        scoped Span<double> smKBuf;
        scoped Span<double> smkUpperBuf;
        scoped Span<double> smkLowerBuf;

        if (len <= StackallocThreshold)
        {
            upperBuf = stackalloc double[len];
            lowerBuf = stackalloc double[len];
            rawKBuf = stackalloc double[len];
            smKBuf = stackalloc double[len];
            smkUpperBuf = stackalloc double[len];
            smkLowerBuf = stackalloc double[len];
        }
        else
        {
            rentedUpper = ArrayPool<double>.Shared.Rent(len);
            rentedLower = ArrayPool<double>.Shared.Rent(len);
            rentedRawK = ArrayPool<double>.Shared.Rent(len);
            rentedSmK = ArrayPool<double>.Shared.Rent(len);
            rentedSmkUpper = ArrayPool<double>.Shared.Rent(len);
            rentedSmkLower = ArrayPool<double>.Shared.Rent(len);
            upperBuf = rentedUpper.AsSpan(0, len);
            lowerBuf = rentedLower.AsSpan(0, len);
            rawKBuf = rentedRawK.AsSpan(0, len);
            smKBuf = rentedSmK.AsSpan(0, len);
            smkUpperBuf = rentedSmkUpper.AsSpan(0, len);
            smkLowerBuf = rentedSmkLower.AsSpan(0, len);
        }

        try
        {
            // Stage 1: raw %K via Highest/Lowest
            Highest.Batch(high, upperBuf, period);
            Lowest.Batch(low, lowerBuf, period);

            double alpha = 2.0 / (period + 1);
            double decay = 1.0 - alpha;

            for (int i = 0; i < len; i++)
            {
                double range = upperBuf[i] - lowerBuf[i];
                rawKBuf[i] = range > 0.0 ? 100.0 * (close[i] - lowerBuf[i]) / range : 0.0;
            }

            // Stage 1 EMA: smooth rawK → smoothK
            smKBuf[0] = rawKBuf[0];
            for (int i = 1; i < len; i++)
            {
                smKBuf[i] = Math.FusedMultiplyAdd(smKBuf[i - 1], decay, alpha * rawKBuf[i]);
            }

            // Stage 2: Highest/Lowest of smoothK
            Highest.Batch(smKBuf.Slice(0, len), smkUpperBuf, period);
            Lowest.Batch(smKBuf.Slice(0, len), smkLowerBuf, period);

            // Stage 2: raw DS
            // Reuse rawKBuf for dsRaw
            for (int i = 0; i < len; i++)
            {
                double skRange = smkUpperBuf[i] - smkLowerBuf[i];
                rawKBuf[i] = skRange > 0.0
                    ? 100.0 * (smKBuf[i] - smkLowerBuf[i]) / skRange
                    : 0.0;
            }

            // Stage 2 EMA: smooth dsRaw → output
            output[0] = rawKBuf[0];
            for (int i = 1; i < len; i++)
            {
                output[i] = Math.FusedMultiplyAdd(output[i - 1], decay, alpha * rawKBuf[i]);
            }
        }
        finally
        {
            if (rentedUpper != null) { ArrayPool<double>.Shared.Return(rentedUpper); }
            if (rentedLower != null) { ArrayPool<double>.Shared.Return(rentedLower); }
            if (rentedRawK != null) { ArrayPool<double>.Shared.Return(rentedRawK); }
            if (rentedSmK != null) { ArrayPool<double>.Shared.Return(rentedSmK); }
            if (rentedSmkUpper != null) { ArrayPool<double>.Shared.Return(rentedSmkUpper); }
            if (rentedSmkLower != null) { ArrayPool<double>.Shared.Return(rentedSmkLower); }
        }
    }

    public static TSeries Batch(TBarSeries source, int period = 21)
    {
        if (source == null || source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var times = new List<long>(len);
        var vals = new List<double>(len);

        CollectionsMarshal.SetCount(times, len);
        CollectionsMarshal.SetCount(vals, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(vals), period);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(times));

        return new TSeries(times, vals);
    }

    public static (TSeries Results, Dstoch Indicator) Calculate(
        TBarSeries source, int period = 21)
    {
        var indicator = new Dstoch(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
