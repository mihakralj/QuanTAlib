using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// KDJ: Enhanced Stochastic Oscillator with K, D, J lines.
/// RSV = 100 * (close - lowestLow) / (highestHigh - lowestLow),
/// K = RMA(RSV, signal), D = RMA(K, signal), J = 3K - 2D.
/// Streaming path uses monotonic deques for O(1) amortized highest/lowest;
/// corrections (isNew=false) rebuild deques without allocations.
/// </summary>
[SkipLocalsInit]
public sealed class Kdj : ITValuePublisher
{
    private readonly int _length;
    private readonly int _signal;
    private readonly double _alpha;
    private readonly double _decay;
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double K, double D, double EK, double ED,
        bool WarmupK, bool WarmupD,
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue K { get; private set; }
    public TValue D { get; private set; }
    public bool IsHot => _count >= _length;

    public event TValuePublishedHandler? Pub;

    public Kdj(int length = 9, int signal = 3)
    {
        if (length <= 0)
        {
            throw new ArgumentException("Length must be greater than 0", nameof(length));
        }
        if (signal <= 0)
        {
            throw new ArgumentException("Signal must be greater than 0", nameof(signal));
        }

        _length = length;
        _signal = signal;
        _alpha = 1.0 / signal;
        _decay = 1.0 - _alpha;
        _hBuf = new double[_length];
        _lBuf = new double[_length];
        _maxDeque = new MonotonicDeque(_length);
        _minDeque = new MonotonicDeque(_length);
        _count = 0;
        _index = -1;
        _s = new State(0.0, 0.0, 1.0, 1.0, true, true, double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"Kdj({length},{signal})";
        WarmupPeriod = length + signal - 1;
        _barHandler = HandleBar;
    }

    public Kdj(TBarSeries source, int length = 9, int signal = 3) : this(length, signal)
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
            if (_count < _length)
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

        // If still no valid data, return NaN
        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            K = new TValue(input.Time, double.NaN);
            D = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        int bufIdx = _index < 0 ? 0 : (int)(_index % _length);
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
        double range = highest - lowest;

        double rsv = range > 0.0 ? 100.0 * (close - lowest) / range : 50.0;

        // RMA smoothing: K = alpha * RSV + decay * prevK
        s.K = Math.FusedMultiplyAdd(s.K, _decay, _alpha * rsv);
        // RMA smoothing: D = alpha * K + decay * prevD
        s.D = Math.FusedMultiplyAdd(s.D, _decay, _alpha * s.K);

        // Exponential warmup compensator for K
        double resultK;
        if (s.WarmupK)
        {
            s.EK *= _decay;
            double cK = 1.0 / (1.0 - s.EK);
            resultK = Math.Clamp(cK * s.K, 0.0, 100.0);
            s.WarmupK = s.EK > 1e-10;
        }
        else
        {
            resultK = Math.Clamp(s.K, 0.0, 100.0);
        }

        // Exponential warmup compensator for D
        double resultD;
        if (s.WarmupD)
        {
            s.ED *= _decay;
            double cD = 1.0 / (1.0 - s.ED);
            resultD = Math.Clamp(cD * s.D, 0.0, 100.0);
            s.WarmupD = s.ED > 1e-10;
        }
        else
        {
            resultD = Math.Clamp(s.D, 0.0, 100.0);
        }

        // J = 3K - 2D (unbounded)
        double j = Math.FusedMultiplyAdd(3.0, resultK, -2.0 * resultD);

        _s = s;

        K = new TValue(input.Time, resultK);
        D = new TValue(input.Time, resultD);
        Last = new TValue(input.Time, j);

        PubEvent(Last, isNew);
        return Last;
    }

    public (TSeries K, TSeries D, TSeries J) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tD = new List<long>(len);
        var vD = new List<double>(len);
        var tJ = new List<long>(len);
        var vJ = new List<double>(len);

        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tD, len);
        CollectionsMarshal.SetCount(vD, len);
        CollectionsMarshal.SetCount(tJ, len);
        CollectionsMarshal.SetCount(vJ, len);

        var vKSpan = CollectionsMarshal.AsSpan(vK);
        var vDSpan = CollectionsMarshal.AsSpan(vD);
        var vJSpan = CollectionsMarshal.AsSpan(vJ);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            vKSpan, vDSpan, vJSpan, _length, _signal);

        var tSpan = CollectionsMarshal.AsSpan(tK);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tD));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tJ));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        K = new TValue(lastTime, vKSpan[^1]);
        D = new TValue(lastTime, vDSpan[^1]);
        Last = new TValue(lastTime, vJSpan[^1]);

        return (new TSeries(tK, vK), new TSeries(tD, vD), new TSeries(tJ, vJ));
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
        _maxDeque.Reset();
        _minDeque.Reset();
        _count = 0;
        _index = -1;
        _s = new State(0.0, 0.0, 1.0, 1.0, true, true, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        Last = default;
        K = default;
        D = default;
    }

    /// <summary>
    /// Batch calculation using spans (zero allocation).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> kOut,
        Span<double> dOut,
        Span<double> jOut,
        int length,
        int signal = 3)
    {
        if (length <= 0)
        {
            throw new ArgumentException("Length must be greater than 0", nameof(length));
        }
        if (signal <= 0)
        {
            throw new ArgumentException("Signal must be greater than 0", nameof(signal));
        }
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length", nameof(high));
        }
        if (kOut.Length < high.Length)
        {
            throw new ArgumentException("K output span must be at least as long as input", nameof(kOut));
        }
        if (dOut.Length < high.Length)
        {
            throw new ArgumentException("D output span must be at least as long as input", nameof(dOut));
        }
        if (jOut.Length < high.Length)
        {
            throw new ArgumentException("J output span must be at least as long as input", nameof(jOut));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 1.0 / signal;
        double decay = 1.0 - alpha;

        // Compute highest/lowest via monotonic deque spans
        const int StackallocThreshold = 256;
        double[]? rentedUpper = null;
        double[]? rentedLower = null;
        scoped Span<double> upperBuf;
        scoped Span<double> lowerBuf;

        if (len <= StackallocThreshold)
        {
            upperBuf = stackalloc double[len];
            lowerBuf = stackalloc double[len];
        }
        else
        {
            rentedUpper = System.Buffers.ArrayPool<double>.Shared.Rent(len);
            rentedLower = System.Buffers.ArrayPool<double>.Shared.Rent(len);
            upperBuf = rentedUpper.AsSpan(0, len);
            lowerBuf = rentedLower.AsSpan(0, len);
        }

        try
        {
            Highest.Batch(high, upperBuf, length);
            Lowest.Batch(low, lowerBuf, length);

            double k = 0.0;
            double d = 0.0;
            double eK = 1.0;
            double eD = 1.0;
            bool warmupK = true;
            bool warmupD = true;

            for (int i = 0; i < len; i++)
            {
                double range = upperBuf[i] - lowerBuf[i];
                double rsv = range > 0.0 ? 100.0 * (close[i] - lowerBuf[i]) / range : 50.0;

                k = Math.FusedMultiplyAdd(k, decay, alpha * rsv);
                d = Math.FusedMultiplyAdd(d, decay, alpha * k);

                double resultK;
                if (warmupK)
                {
                    eK *= decay;
                    double cK = 1.0 / (1.0 - eK);
                    resultK = Math.Clamp(cK * k, 0.0, 100.0);
                    warmupK = eK > 1e-10;
                }
                else
                {
                    resultK = Math.Clamp(k, 0.0, 100.0);
                }

                double resultD;
                if (warmupD)
                {
                    eD *= decay;
                    double cD = 1.0 / (1.0 - eD);
                    resultD = Math.Clamp(cD * d, 0.0, 100.0);
                    warmupD = eD > 1e-10;
                }
                else
                {
                    resultD = Math.Clamp(d, 0.0, 100.0);
                }

                kOut[i] = resultK;
                dOut[i] = resultD;
                jOut[i] = Math.FusedMultiplyAdd(3.0, resultK, -2.0 * resultD);
            }
        }
        finally
        {
            if (rentedUpper != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedUpper);
            }
            if (rentedLower != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedLower);
            }
        }
    }

    public static (TSeries K, TSeries D, TSeries J) Batch(TBarSeries source, int length = 9, int signal = 3)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tD = new List<long>(len);
        var vD = new List<double>(len);
        var tJ = new List<long>(len);
        var vJ = new List<double>(len);

        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tD, len);
        CollectionsMarshal.SetCount(vD, len);
        CollectionsMarshal.SetCount(tJ, len);
        CollectionsMarshal.SetCount(vJ, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(vK),
            CollectionsMarshal.AsSpan(vD),
            CollectionsMarshal.AsSpan(vJ),
            length, signal);

        var tSpan = CollectionsMarshal.AsSpan(tK);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tD));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tJ));

        return (new TSeries(tK, vK), new TSeries(tD, vD), new TSeries(tJ, vJ));
    }

    public static ((TSeries K, TSeries D, TSeries J) Results, Kdj Indicator) Calculate(TBarSeries source, int length = 9, int signal = 3)
    {
        var indicator = new Kdj(length, signal);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
