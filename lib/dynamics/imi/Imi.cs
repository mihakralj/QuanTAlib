// IMI: Intraday Momentum Index
// Developed by Tushar Chande
// Combines candlestick analysis with RSI-like calculation
// Uses gain/loss based on intraday Open-Close relationship

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// IMI: Intraday Momentum Index
/// </summary>
/// <remarks>
/// A technical indicator developed by Tushar Chande that combines candlestick analysis
/// with RSI-like overbought/oversold signals. Unlike RSI which uses close-to-close changes,
/// IMI uses the relationship between each bar's open and close prices.
///
/// Calculation:
/// <c>Gain = Close - Open (when Close > Open, otherwise 0)</c>
/// <c>Loss = Open - Close (when Close &lt; Open, otherwise 0)</c>
/// <c>IMI = 100 × Sum(Gains, n) / (Sum(Gains, n) + Sum(Losses, n))</c>
///
/// Key Levels:
/// - Above 70: Overbought condition
/// - Below 30: Oversold condition
/// - 50: Neutral (equal up and down momentum)
///
/// Sources:
/// - Investopedia: https://www.investopedia.com/terms/i/intraday-momentum-index-imi.asp
/// - CQG: https://help.cqg.com/cqgic/25/Documents/intradaymomentumindeximi.htm
/// </remarks>
[SkipLocalsInit]
public sealed class Imi : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _gains;
    private readonly RingBuffer _losses;

    // Rolling sums for O(1) updates
    private double _gainSum;
    private double _lossSum;

    // Bar correction state
    private double _savedGainSum;
    private double _savedLossSum;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Event publisher for value updates.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current IMI value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has enough data for a full period calculation.
    /// </summary>
    public bool IsHot => _gains.IsFull;

    /// <summary>
    /// The period parameter.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates IMI indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 1)</param>
    public Imi(int period = 14)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        _period = period;
        Name = $"IMI({period})";
        WarmupPeriod = period;

        _gains = new RingBuffer(period);
        _losses = new RingBuffer(period);

        _gainSum = 0.0;
        _lossSum = 0.0;
        _savedGainSum = 0.0;
        _savedLossSum = 0.0;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _gains.Clear();
        _losses.Clear();
        _gainSum = 0.0;
        _lossSum = 0.0;
        _savedGainSum = 0.0;
        _savedLossSum = 0.0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>
    /// Updates the IMI indicator with a new bar.
    /// </summary>
    /// <param name="input">The price bar (Open, Close required)</param>
    /// <param name="isNew">True for new bar, false for update of current bar</param>
    /// <returns>The current IMI value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double open = input.Open;
        double close = input.Close;

        // Handle NaN/Infinity inputs
        if (!double.IsFinite(open) || !double.IsFinite(close))
        {
            PubEvent(Last, isNew);
            return Last;
        }

        if (isNew)
        {
            // Save state for potential correction
            _savedGainSum = _gainSum;
            _savedLossSum = _lossSum;
        }
        else
        {
            // Restore state for correction
            _gainSum = _savedGainSum;
            _lossSum = _savedLossSum;
        }

        // Calculate gain and loss for this bar
        double gain = 0.0;
        double loss = 0.0;

        if (close > open)
        {
            gain = close - open;
        }
        else if (close < open)
        {
            loss = open - close;
        }
        // When close == open, both gain and loss remain 0

        // Update rolling sums: subtract old value if buffer is full
        if (_gains.IsFull)
        {
            _gainSum -= _gains[0];
            _lossSum -= _losses[0];
        }

        // Add new values to buffers
        _gains.Add(gain, isNew);
        _losses.Add(loss, isNew);
        _gainSum += gain;
        _lossSum += loss;

        // Calculate IMI
        double total = _gainSum + _lossSum;
        double imi = total > 0 ? 100.0 * _gainSum / total : 50.0;

        Last = new TValue(input.Time, imi);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Calculates IMI for the entire bar series.
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

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            Update(bar, isNew: true);
            tList.Add(bar.Time);
            vList.Add(Last.Value);
        }

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Primes the indicator with historical bar data.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Calculates IMI for the entire bar series using default parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source)
    {
        var imi = new Imi();
        return imi.Update(source);
    }

    /// <summary>
    /// Calculates IMI for the entire bar series using custom period.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period)
    {
        var imi = new Imi(period);
        return imi.Update(source);
    }

    /// <summary>
    /// Calculates IMI and returns both results and the warm indicator.
    /// </summary>
    public static (TSeries Results, Imi Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var imi = new Imi(period);
        var results = imi.Update(source);
        return (results, imi);
    }
}
