// QSTICK: Qstick Indicator by Tushar Chande
// Measures average candlestick body: MA(Close - Open)
// Positive = bullish (closes above opens), Negative = bearish

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Qstick (QSTICK) - Candlestick Momentum Indicator
/// A moving average of the difference between Close and Open prices,
/// measuring the average direction and strength of candlestick bodies.
///
/// Calculation: Qstick = MA(Close - Open, period)
/// </summary>
/// <remarks>
/// <b>Calculation:</b>
/// <code>
/// diff = Close - Open
/// Qstick = SMA(diff, period)  or  EMA(diff, period)
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) update complexity per bar
/// - Supports SMA or EMA averaging modes
/// - Positive = average bullish bars
/// - Negative = average bearish bars
/// - Uses RingBuffer for SMA (handles isNew internally)
/// - Uses state rollback for EMA bar correction support
/// </remarks>
/// <seealso href="Qstick.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Qstick : ITValuePublisher
{
    private const int DefaultPeriod = 14;
    private const bool DefaultUseEma = false;

    private readonly int _period;
    private readonly bool _useEma;
    private readonly double _alpha;
    private readonly RingBuffer _buffer;

    // State for bar correction (EMA mode only)
    private double _emaValue;
    private double _savedEmaValue;
    private int _count;
    private int _savedCount;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current Qstick value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True when the indicator has calculated a valid value.
    /// For SMA: after receiving 'period' bars
    /// For EMA: after receiving at least 1 bar (with bias compensation approximation)
    /// </summary>
    public bool IsHot => _useEma ? _count > 0 : _buffer.IsFull;

    /// <summary>
    /// The lookback period parameter.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Whether the indicator uses EMA (true) or SMA (false).
    /// </summary>
    public bool UseEma => _useEma;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates a Qstick indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 1)</param>
    /// <param name="useEma">Use EMA (true) or SMA (false)</param>
    public Qstick(int period = DefaultPeriod, bool useEma = DefaultUseEma)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        _period = period;
        _useEma = useEma;
        _alpha = 2.0 / (period + 1);
        Name = useEma ? $"QSTICK({period},EMA)" : $"QSTICK({period})";
        WarmupPeriod = period;

        if (!useEma)
        {
            _buffer = new RingBuffer(period);
        }
        else
        {
            _buffer = null!;
        }

        _emaValue = 0;
        _savedEmaValue = 0;
        _count = 0;
        _savedCount = 0;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _buffer?.Clear();
        _emaValue = 0;
        _savedEmaValue = 0;
        _count = 0;
        _savedCount = 0;
        Last = default;
    }

    /// <summary>
    /// Updates the Qstick indicator with a new bar.
    /// </summary>
    /// <param name="input">The price bar (Open and Close required)</param>
    /// <param name="isNew">True for new bar, false for update of current bar</param>
    /// <returns>The current Qstick value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double open = input.Open;
        double close = input.Close;

        // Handle NaN/Infinity inputs
        if (!double.IsFinite(open) || !double.IsFinite(close))
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
            return Last;
        }

        double diff = close - open;

        double result;
        if (_useEma)
        {
            if (isNew)
            {
                _savedEmaValue = _emaValue;
                _savedCount = _count;
            }
            else
            {
                _emaValue = _savedEmaValue;
                _count = _savedCount;
            }

            // EMA calculation
            if (_count == 0)
            {
                _emaValue = diff;
            }
            else
            {
                _emaValue = Math.FusedMultiplyAdd(_alpha, diff - _emaValue, _emaValue);
            }

            if (isNew)
            {
                _count++;
            }

            result = _emaValue;
        }
        else
        {
            // SMA calculation using RingBuffer
            // RingBuffer.Add handles isNew internally:
            // - isNew=true: adds new value, removes oldest if full
            // - isNew=false: replaces newest value
            // RingBuffer.Sum is always accurate after Add
            _buffer.Add(diff, isNew);

            int count = _buffer.Count;
            result = count > 0 ? _buffer.Sum / count : double.NaN;
        }

        Last = new TValue(input.Time, result);
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
    /// Creates and returns results for a bar series.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = DefaultPeriod, bool useEma = DefaultUseEma)
    {
        var indicator = new Qstick(period, useEma);
        return indicator.Update(source);
    }

    /// <summary>
    /// Returns the indicator and its results.
    /// </summary>
    public static (TSeries Results, Qstick Indicator) Calculate(
        TBarSeries source,
        int period = DefaultPeriod,
        bool useEma = DefaultUseEma)
    {
        var indicator = new Qstick(period, useEma);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
