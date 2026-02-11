using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// AROONOSC: Aroon Oscillator
/// </summary>
/// <remarks>
/// Single-line trend indicator derived from Aroon Up minus Aroon Down (Chande).
/// Range [-100, +100]: positive = uptrend, negative = downtrend.
///
/// Calculation: <c>AroonOsc = AroonUp - AroonDown</c>.
/// </remarks>
/// <seealso href="AroonOsc.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class AroonOsc : ITValuePublisher
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
    /// Current Aroon Oscillator value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has enough data for a full period calculation.
    /// </summary>
    public bool IsHot => _highs.IsFull;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates Aroon Oscillator with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be > 0)</param>
    public AroonOsc(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        Name = $"AroonOsc({period})";
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

        Batch(source.High.Values, source.Low.Values, period: _period, destination: v);

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
    /// Calculates Aroon oscillator values using the shared O(n) algorithm from Aroon.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="period">Lookback period</param>
    /// <param name="destination">Output oscillator values (Up - Down)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, int period, Span<double> destination)
    {
        // Delegate to Aroon's O(n) monotonic deque implementation
        Aroon.Batch(high, low, period, destination);
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

        Batch(source.High.Values, source.Low.Values, period, v);

        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        return new TSeries(tList, [.. v]);
    }

    public static (TSeries Results, AroonOsc Indicator) Calculate(TBarSeries source, int period)
    {
        var indicator = new AroonOsc(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
