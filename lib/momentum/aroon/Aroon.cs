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

    public event Action<TValue>? Pub;

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
        int daysSinceHigh = (count - 1) - maxIdxRelative;
        int daysSinceLow = (count - 1) - minIdxRelative;

        double up = ((double)(_period - daysSinceHigh) / _period) * 100.0;
        double down = ((double)(_period - daysSinceLow) / _period) * 100.0;
        double osc = up - down;

        Up = new TValue(input.Time, up);
        Down = new TValue(input.Time, down);
        Last = new TValue(input.Time, osc);

        Pub?.Invoke(Last);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    public static TSeries Batch(TBarSeries source, int period)
    {
        var aroon = new Aroon(period);
        return aroon.Update(source);
    }
}
