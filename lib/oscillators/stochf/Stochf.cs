// STOCHF: Stochastic Fast Oscillator
// Fast %K = 100 * (close - lowestLow) / (highestHigh - lowestLow)
// Fast %D = SMA(Fast %K, dPeriod)

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// STOCHF: Stochastic Fast Oscillator (%K and %D).
/// %K = 100 * (close - lowestLow) / (highestHigh - lowestLow).
/// %D = SMA(%K, dPeriod).
/// Unsmoothed variant of the Stochastic Oscillator — %K is raw (no additional smoothing).
/// Streaming path uses monotonic deques for O(1) amortized highest/lowest;
/// %D uses a circular buffer with running sum for O(1) SMA.
/// </summary>
[SkipLocalsInit]
public sealed class Stochf : ITValuePublisher
{
    private const int DefaultKLength = 5;
    private const int DefaultDPeriod = 3;

    private readonly int _kLength;
    private readonly int _dPeriod;
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly double[] _dBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double DSum, int DHead, double PrevDVal,
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue K { get; private set; }
    public TValue D { get; private set; }
    public bool IsHot => _count >= _kLength;

    public event TValuePublishedHandler? Pub;

    public Stochf(int kLength = DefaultKLength, int dPeriod = DefaultDPeriod)
    {
        if (kLength <= 0)
        {
            throw new ArgumentException("K length must be greater than 0", nameof(kLength));
        }
        if (dPeriod <= 0)
        {
            throw new ArgumentException("D period must be greater than 0", nameof(dPeriod));
        }

        _kLength = kLength;
        _dPeriod = dPeriod;
        _hBuf = new double[_kLength];
        _lBuf = new double[_kLength];
        _dBuf = new double[_dPeriod];
        _maxDeque = new MonotonicDeque(_kLength);
        _minDeque = new MonotonicDeque(_kLength);
        _count = 0;
        _index = -1;
        _s = new State(0.0, 0, 0.0, double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"StochF({kLength},{dPeriod})";
        WarmupPeriod = kLength;
        _barHandler = HandleBar;
    }

    public Stochf(TBarSeries source, int kLength = DefaultKLength, int dPeriod = DefaultDPeriod)
        : this(kLength, dPeriod)
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
            if (_count < _kLength)
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

        int bufIdx = _index < 0 ? 0 : (int)(_index % _kLength);
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

        double kVal = range > 0.0 ? 100.0 * (close - lowest) / range : 0.0;

        // SMA of %K for %D using circular buffer + running sum
        if (_index == 0)
        {
            // First bar: fill entire buffer with kVal
            for (int i = 0; i < _dPeriod; i++)
            {
                _dBuf[i] = kVal;
            }
            s.DSum = kVal * _dPeriod;
            s.DHead = 0;
            s.PrevDVal = kVal;
        }
        else
        {
            int dIdx = s.DHead;
            s.PrevDVal = _dBuf[dIdx];
            s.DSum = s.DSum - s.PrevDVal + kVal;
            _dBuf[dIdx] = kVal;
            if (isNew)
            {
                s.DHead = (dIdx + 1) % _dPeriod;
            }
        }

        double dVal = s.DSum / _dPeriod;

        _s = s;

        K = new TValue(input.Time, kVal);
        D = new TValue(input.Time, dVal);
        Last = new TValue(input.Time, kVal);

        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    public (TSeries K, TSeries D) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tD = new List<long>(len);
        var vD = new List<double>(len);

        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tD, len);
        CollectionsMarshal.SetCount(vD, len);

        var vKSpan = CollectionsMarshal.AsSpan(vK);
        var vDSpan = CollectionsMarshal.AsSpan(vD);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            vKSpan, vDSpan, _kLength, _dPeriod);

        var tSpan = CollectionsMarshal.AsSpan(tK);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tD));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        K = new TValue(lastTime, vKSpan[^1]);
        D = new TValue(lastTime, vDSpan[^1]);
        Last = new TValue(lastTime, vKSpan[^1]);

        return (new TSeries(tK, vK), new TSeries(tD, vD));
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
        Array.Clear(_dBuf);
        _maxDeque.Reset();
        _minDeque.Reset();
        _count = 0;
        _index = -1;
        _s = new State(0.0, 0, 0.0, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        Last = default;
        K = default;
        D = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> kOut,
        Span<double> dOut,
        int kLength,
        int dPeriod = DefaultDPeriod)
    {
        if (kLength <= 0)
        {
            throw new ArgumentException("K length must be greater than 0", nameof(kLength));
        }
        if (dPeriod <= 0)
        {
            throw new ArgumentException("D period must be greater than 0", nameof(dPeriod));
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

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Compute highest/lowest via Highest/Lowest batch helpers
        const int StackallocThreshold = 256;
        double[]? rentedUpper = null;
        double[]? rentedLower = null;
        double[]? rentedDBuf = null;
        scoped Span<double> upperBuf;
        scoped Span<double> lowerBuf;

        if (len <= StackallocThreshold)
        {
            upperBuf = stackalloc double[len];
            lowerBuf = stackalloc double[len];
        }
        else
        {
            rentedUpper = ArrayPool<double>.Shared.Rent(len);
            rentedLower = ArrayPool<double>.Shared.Rent(len);
            upperBuf = rentedUpper.AsSpan(0, len);
            lowerBuf = rentedLower.AsSpan(0, len);
        }

        // SMA circular buffer for %D
        scoped Span<double> dBuf;
        if (dPeriod <= StackallocThreshold)
        {
            dBuf = stackalloc double[dPeriod];
        }
        else
        {
            rentedDBuf = ArrayPool<double>.Shared.Rent(dPeriod);
            dBuf = rentedDBuf.AsSpan(0, dPeriod);
        }
        dBuf.Clear();

        try
        {
            Highest.Batch(high, upperBuf, kLength);
            Lowest.Batch(low, lowerBuf, kLength);

            double dSum = 0.0;
            int dHead = 0;

            for (int i = 0; i < len; i++)
            {
                double range = upperBuf[i] - lowerBuf[i];
                double kVal = range > 0.0 ? 100.0 * (close[i] - lowerBuf[i]) / range : 0.0;

                kOut[i] = kVal;

                if (i == 0)
                {
                    // Fill entire D buffer with first %K value
                    for (int j = 0; j < dPeriod; j++)
                    {
                        dBuf[j] = kVal;
                    }
                    dSum = kVal * dPeriod;
                    dHead = 0;
                }
                else
                {
                    double oldVal = dBuf[dHead];
                    dSum = dSum - oldVal + kVal;
                    dBuf[dHead] = kVal;
                    dHead = (dHead + 1) % dPeriod;
                }

                dOut[i] = dSum / dPeriod;
            }
        }
        finally
        {
            if (rentedUpper != null)
            {
                ArrayPool<double>.Shared.Return(rentedUpper);
            }
            if (rentedLower != null)
            {
                ArrayPool<double>.Shared.Return(rentedLower);
            }
            if (rentedDBuf != null)
            {
                ArrayPool<double>.Shared.Return(rentedDBuf);
            }
        }
    }

    public static (TSeries K, TSeries D) Batch(TBarSeries source,
        int kLength = DefaultKLength, int dPeriod = DefaultDPeriod)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tD = new List<long>(len);
        var vD = new List<double>(len);

        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tD, len);
        CollectionsMarshal.SetCount(vD, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(vK),
            CollectionsMarshal.AsSpan(vD),
            kLength, dPeriod);

        var tSpan = CollectionsMarshal.AsSpan(tK);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tD));

        return (new TSeries(tK, vK), new TSeries(tD, vD));
    }

    public static ((TSeries K, TSeries D) Results, Stochf Indicator) Calculate(
        TBarSeries source, int kLength = DefaultKLength, int dPeriod = DefaultDPeriod)
    {
        var indicator = new Stochf(kLength, dPeriod);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
