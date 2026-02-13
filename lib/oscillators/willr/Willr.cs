using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// WILLR: Williams %R.
/// Measures close position relative to highest high over a lookback period.
/// Range: -100 (lowest low) to 0 (highest high).
/// Formula: WillR = -100 * (HighestHigh - Close) / (HighestHigh - LowestLow).
/// When range is zero, returns -50 (midpoint).
/// Uses monotonic deques for O(1) amortized highest/lowest tracking.
/// </summary>
[SkipLocalsInit]
public sealed class Willr : ITValuePublisher
{
    private const int DefaultPeriod = 14;

    private readonly int _period;
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int Period => _period;
    public int WarmupPeriod => _period;
    public TValue Last { get; private set; }
    public bool IsHot => _count >= _period;

    public event TValuePublishedHandler? Pub;

    public Willr(int period = DefaultPeriod)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _hBuf = new double[_period];
        _lBuf = new double[_period];
        _maxDeque = new MonotonicDeque(_period);
        _minDeque = new MonotonicDeque(_period);
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"WillR({period})";
        _barHandler = HandleBar;
    }

    public Willr(TBarSeries source, int period = DefaultPeriod) : this(period)
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

        // If still no valid data, return NaN
        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

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
        double range = highest - lowest;

        double willr = range > 0.0 ? -100.0 * (highest - close) / range : -50.0;

        _s = s;

        Last = new TValue(input.Time, willr);
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
        var t = new List<long>(len);
        var v = new List<double>(len);

        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), _period);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, CollectionsMarshal.AsSpan(v)[^1]);

        return new TSeries(t, v);
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

    public void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();

        if (source.Length == 0)
        {
            return;
        }

        long t = DateTime.UtcNow.Ticks;
        long stepTicks = (step ?? TimeSpan.FromMinutes(1)).Ticks;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            Update(new TBar(t, val, val, val, val, 0), isNew: true);
            t += stepTicks;
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
        _s = new State(double.NaN, double.NaN, double.NaN);
        _ps = _s;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period)
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

        // Compute highest/lowest via Highest/Lowest batch helpers
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
            rentedUpper = ArrayPool<double>.Shared.Rent(len);
            rentedLower = ArrayPool<double>.Shared.Rent(len);
            upperBuf = rentedUpper.AsSpan(0, len);
            lowerBuf = rentedLower.AsSpan(0, len);
        }

        try
        {
            Highest.Batch(high, upperBuf, period);
            Lowest.Batch(low, lowerBuf, period);

            for (int i = 0; i < len; i++)
            {
                double range = upperBuf[i] - lowerBuf[i];
                output[i] = range > 0.0 ? -100.0 * (upperBuf[i] - close[i]) / range : -50.0;
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
        }
    }

    public static TSeries Batch(TBarSeries source, int period = DefaultPeriod)
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

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), period);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    public static (TSeries Results, Willr Indicator) Calculate(
        TBarSeries source, int period = DefaultPeriod)
    {
        var indicator = new Willr(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
