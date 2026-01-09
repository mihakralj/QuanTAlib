using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Aroon Indicator
/// </summary>
/// <remarks>
/// The Aroon indicator is used to identify trend changes in the price of an asset, as well as the strength of that trend.
/// It consists of two lines: Aroon Up and Aroon Down.
///
/// Calculation:
/// Aroon Up = ((Period - Days Since Period High) / Period) * 100
/// Aroon Down = ((Period - Days Since Period Low) / Period) * 100
/// Aroon Oscillator = Aroon Up - Aroon Down
///
/// The indicator requires Period + 1 samples to fully calculate "Period" days ago.
///
/// Sources:
/// https://www.investopedia.com/terms/a/aroon.asp
/// Tushar Chande (1995)
/// </remarks>
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
            throw new ArgumentException("Period must be greater than 0", nameof(period));

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
        if (source.Count == 0) return new TSeries([], []);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        for (int i = 0; i < len; i++)
        {
            int windowStart = i - Math.Min(i, period);

            double maxVal = double.MinValue;
            int maxIdx = windowStart;
            double minVal = double.MaxValue;
            int minIdx = windowStart;

            for (int j = windowStart; j <= i; j++)
            {
                double h = high[j];
                if (h >= maxVal)
                {
                    maxVal = h;
                    maxIdx = j;
                }

                double l = low[j];
                if (l <= minVal)
                {
                    minVal = l;
                    minIdx = j;
                }
            }

            int daysSinceHigh = i - maxIdx;
            int daysSinceLow = i - minIdx;

            double up = ((double)(period - daysSinceHigh) / period) * 100.0;
            double down = ((double)(period - daysSinceLow) / period) * 100.0;
            destination[i] = up - down;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSeries Batch(TBarSeries source, int period)
    {
        if (source.Count == 0) return new TSeries([], []);

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
