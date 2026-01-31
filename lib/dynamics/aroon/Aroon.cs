using System.Buffers;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// AROON: Aroon Indicator
/// </summary>
/// <remarks>
/// Trend timing indicator measuring bars since period high/low (Chande).
/// Outputs Up [0-100], Down [0-100], and Oscillator (Up - Down).
///
/// Calculation: <c>Up = (Period - DaysSinceHigh) / Period × 100</c>; <c>Down = (Period - DaysSinceLow) / Period × 100</c>.
/// </remarks>
/// <seealso href="Aroon.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Aroon : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _highs;
    private readonly RingBuffer _lows;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current Aroon Oscillator value (Up - Down).
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current Aroon Up value.
    /// </summary>
    public TValue Up { get; private set; }

    /// <summary>
    /// Current Aroon Down value.
    /// </summary>
    public TValue Down { get; private set; }

    /// <summary>
    /// True if the indicator has enough data for a full period calculation.
    /// </summary>
    public bool IsHot => _highs.IsFull;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates Aroon indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be > 0)</param>
    public Aroon(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        Name = $"Aroon({period})";
        WarmupPeriod = period;
        // We need Period + 1 samples to cover the range [0, Period] days ago.
        _highs = new RingBuffer(period + 1);
        _lows = new RingBuffer(period + 1);
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        Last = default;
        Up = default;
        Down = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        _highs.Add(input.High, isNew);
        _lows.Add(input.Low, isNew);

        if (_highs.Count == 0)
        {
            return default;
        }

        // Find max index in highs (Zero allocation)
        var highsBuffer = _highs.InternalBuffer;
        int count = _highs.Count;
        int capacity = _highs.Capacity;
        int start = _highs.StartIndex;

        double maxVal = double.MinValue;
        int maxIdxRelative = 0;

        for (int i = 0; i < count; i++)
        {
            int idx = (start + i) % capacity;
            double val = highsBuffer[idx];
            // Use >= to find the most recent high if values are equal
            if (val >= maxVal)
            {
                maxVal = val;
                maxIdxRelative = i;
            }
        }

        // Find min index in lows (Zero allocation)
        var lowsBuffer = _lows.InternalBuffer;
        double minVal = double.MaxValue;
        int minIdxRelative = 0;

        for (int i = 0; i < count; i++)
        {
            int idx = (start + i) % capacity;
            double val = lowsBuffer[idx];
            // Use <= to find the most recent low if values are equal
            if (val <= minVal)
            {
                minVal = val;
                minIdxRelative = i;
            }
        }

        // Calculate days since (0 means current bar is the high/low)
        int daysSinceHigh = count - 1 - maxIdxRelative;
        int daysSinceLow = count - 1 - minIdxRelative;

        double up = ((double)(_period - daysSinceHigh) / _period) * 100.0;
        double down = ((double)(_period - daysSinceLow) / _period) * 100.0;
        double osc = up - down;

        Up = new TValue(input.Time, up);
        Down = new TValue(input.Time, down);
        Last = new TValue(input.Time, osc);

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Calculate(source.High.Values, source.Low.Values, _period, v);

        var tList = new List<long>(len);
        var vList = new List<double>(v);

        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Calculates Aroon oscillator values using O(n) monotonic deque algorithm.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="period">Lookback period</param>
    /// <param name="destination">Output oscillator values (Up - Down)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low, int period, Span<double> destination)
    {
        int len = high.Length;
        if (len == 0 || len != low.Length || len != destination.Length || period <= 0)
        {
            if (destination.Length > 0)
            {
                destination.Clear();
            }
            return;
        }

        // Use monotonic deques for O(n) complexity
        // Deque stores indices; front has the max/min index within the window
        // Max deque size is bounded by window size (period + 1), but we use circular indexing
        int windowSize = period + 1;
        int[]? rented = ArrayPool<int>.Shared.Rent(windowSize * 2);
        try
        {
            Span<int> buffer = rented.AsSpan(0, windowSize * 2);
            Span<int> maxDeque = buffer.Slice(0, windowSize);  // circular buffer for max indices
            Span<int> minDeque = buffer.Slice(windowSize, windowSize);  // circular buffer for min indices

            int maxHead = 0, maxTail = 0, maxCount = 0;  // circular deque for highs
            int minHead = 0, minTail = 0, minCount = 0;  // circular deque for lows

            double invPeriod = 100.0 / period;

            for (int i = 0; i < len; i++)
            {
                // Remove elements outside the window [i - period, i]
                int windowStart = i - period;

                // Remove old indices from front of max deque
                while (maxCount > 0 && maxDeque[maxHead] < windowStart)
                {
                    maxHead = (maxHead + 1) % windowSize;
                    maxCount--;
                }

                // Remove old indices from front of min deque
                while (minCount > 0 && minDeque[minHead] < windowStart)
                {
                    minHead = (minHead + 1) % windowSize;
                    minCount--;
                }

                // Add current index to max deque (maintain decreasing order)
                // Use <= to keep most recent max when values equal
                double h = high[i];
                while (maxCount > 0 && high[maxDeque[(maxTail - 1 + windowSize) % windowSize]] <= h)
                {
                    maxTail = (maxTail - 1 + windowSize) % windowSize;
                    maxCount--;
                }
                maxDeque[maxTail] = i;
                maxTail = (maxTail + 1) % windowSize;
                maxCount++;

                // Add current index to min deque (maintain increasing order)
                // Use >= to keep most recent min when values equal
                double l = low[i];
                while (minCount > 0 && low[minDeque[(minTail - 1 + windowSize) % windowSize]] >= l)
                {
                    minTail = (minTail - 1 + windowSize) % windowSize;
                    minCount--;
                }
                minDeque[minTail] = i;
                minTail = (minTail + 1) % windowSize;
                minCount++;

                // Calculate Aroon values
                int maxIdx = maxDeque[maxHead];
                int minIdx = minDeque[minHead];

                int daysSinceHigh = i - maxIdx;
                int daysSinceLow = i - minIdx;

                double up = (period - daysSinceHigh) * invPeriod;
                double down = (period - daysSinceLow) * invPeriod;
                destination[i] = up - down;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSeries Batch(TBarSeries source, int period)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Calculate(source.High.Values, source.Low.Values, period, v);

        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        return new TSeries(tList, [.. v]);
    }
}
