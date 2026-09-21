using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// CHOP: Choppiness Index
/// </summary>
/// <remarks>
/// Non-directional indicator measuring market trendiness (E.W. Dreiss).
/// Range [0-100]: Low values indicate trending, high values indicate choppy/sideways markets.
///
/// Calculation: <c>CHOP = 100 × LOG10(SUM(TR, n) / (MaxHigh - MinLow)) / LOG10(n)</c>.
///
/// Key Levels:
/// - Above 61.8: Market is consolidating (choppy)
/// - Below 38.2: Market is trending
/// - 50: Neutral midpoint
/// </remarks>
/// <seealso href="Chop.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Chop : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _trValues;
    private readonly RingBuffer _highs;
    private readonly RingBuffer _lows;

    // Bar correction state
    private double _trSum;
    private double _savedTrSum;
    private double _prevClose;
    private double _savedPrevClose;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current CHOP value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has enough data for a full period calculation.
    /// </summary>
    public bool IsHot => _trValues.IsFull;

    /// <summary>
    /// The period parameter.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates CHOP indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 2)</param>
    public Chop(int period = 14)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }

        _period = period;
        Name = $"CHOP({period})";
        WarmupPeriod = period;

        _trValues = new RingBuffer(period);
        _highs = new RingBuffer(period);
        _lows = new RingBuffer(period);

        _trSum = 0.0;
        _savedTrSum = 0.0;
        _prevClose = double.NaN;
        _savedPrevClose = double.NaN;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _trValues.Clear();
        _highs.Clear();
        _lows.Clear();
        _trSum = 0.0;
        _savedTrSum = 0.0;
        _prevClose = double.NaN;
        _savedPrevClose = double.NaN;
        Last = default;
    }

    /// <summary>
    /// Updates the CHOP indicator with a new bar.
    /// </summary>
    /// <param name="input">The price bar (High, Low, Close required)</param>
    /// <param name="isNew">True for new bar, false for update of current bar</param>
    /// <returns>The current CHOP value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        // Handle NaN/Infinity inputs
        if (!double.IsFinite(high) || !double.IsFinite(low) || !double.IsFinite(close))
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
            return Last;
        }

        if (isNew)
        {
            // Save state for potential correction
            _savedTrSum = _trSum;
            _savedPrevClose = _prevClose;
        }
        else
        {
            // Restore state for correction
            _trSum = _savedTrSum;
            _prevClose = _savedPrevClose;
        }

        // Calculate True Range
        double pc = double.IsNaN(_prevClose) ? close : _prevClose;
        double tr = Math.Max(high - low, Math.Max(Math.Abs(high - pc), Math.Abs(low - pc)));

        // Update rolling sum: subtract old value if buffer is full
        if (_trValues.IsFull)
        {
            _trSum -= _trValues[0];
        }

        // Add new values to buffers
        _trValues.Add(tr, isNew);
        _highs.Add(high, isNew);
        _lows.Add(low, isNew);
        _trSum += tr;

        // Update previous close for next bar
        if (isNew)
        {
            _prevClose = close;
        }

        // Calculate CHOP if we have enough data
        double chop = ComputeChop();

        Last = new TValue(input.Time, chop);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates with a bar series.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var tList = new List<long>(len);
        var vList = new List<double>(len);

        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i], isNew: true);
            tList.Add(times[i]);
            vList.Add(result.Value);
        }

        return new TSeries(tList, vList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeChop()
    {
        int count = _trValues.Count;
        if (count < 2)
        {
            return double.NaN;
        }

        // Find max high and min low in the period
        double maxHigh = double.MinValue;
        double minLow = double.MaxValue;

        var highsBuffer = _highs.InternalBuffer;
        var lowsBuffer = _lows.InternalBuffer;
        int capacity = _highs.Capacity;
        int start = _highs.StartIndex;

        for (int i = 0; i < count; i++)
        {
            int idx = (start + i) % capacity;
            double h = highsBuffer[idx];
            double l = lowsBuffer[idx];

            if (h > maxHigh)
            {
                maxHigh = h;
            }

            if (l < minLow)
            {
                minLow = l;
            }
        }

        double priceRange = maxHigh - minLow;

        // Avoid division by zero
        if (priceRange <= 0.0)
        {
            return double.NaN;
        }

        // CHOP = 100 * LOG10(SUM_TR / RANGE) / LOG10(n)
        double logRatio = Math.Log10(_trSum / priceRange);
        double logN = Math.Log10(count);

        double chop = 100.0 * logRatio / logN;

        // Clamp to [0, 100]
        return Math.Clamp(chop, 0.0, 100.0);
    }

    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    /// <param name="source">Historical bar data.</param>
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

    /// <summary>
    /// Batch calculation with default parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source)
    {
        return Batch(source, period: 14);
    }

    /// <summary>
    /// Batch calculation with specified parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period)
    {
        var indicator = new Chop(period);
        return indicator.Update(source);
    }

    public static (TSeries Results, Chop Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Chop();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}