using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// WAD: Williams Accumulation/Distribution
/// </summary>
/// <remarks>
/// Uses True Range concepts and volume to measure buying and selling pressure based on
/// close position relative to previous close. Rising WAD confirms accumulation; falling confirms distribution.
///
/// Calculation: <c>TRH = max(High, prev_Close)</c>, <c>TRL = min(Low, prev_Close)</c>,
/// <c>PM = Close - TRL (if up), Close - TRH (if down), 0 (unchanged)</c>,
/// <c>WAD = cumulative sum(PM × Volume)</c>.
/// </remarks>
/// <seealso href="Wad.md">Detailed documentation</seealso>
/// <seealso href="wad.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Wad : ITValuePublisher
{
    private double _wad;
    private double _p_wad;
    private double _prevClose;
    private double _p_prevClose;
    private bool _isInitialized;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public static string Name => "WAD";

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current WAD value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Minimum number of data points required before the indicator becomes valid.
    /// </summary>
    public int WarmupPeriod { get; } = 1;

    /// <summary>
    /// True if the indicator has processed at least one bar.
    /// </summary>
    public bool IsHot => _isInitialized;

    /// <summary>
    /// Creates a new WAD indicator.
    /// </summary>
    public Wad()
    {
        _isInitialized = false;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _wad = 0;
        _p_wad = 0;
        _prevClose = 0;
        _p_prevClose = 0;
        _isInitialized = false;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_wad = _wad;
            _p_prevClose = _prevClose;
        }
        else
        {
            _wad = _p_wad;
            _prevClose = _p_prevClose;
        }

        double close = input.Close;
        double high = input.High;
        double low = input.Low;
        double volume = input.Volume;

        if (!_isInitialized)
        {
            // First bar: no previous close, WAD starts at 0
            _prevClose = close;
            _isInitialized = true;
            Last = new TValue(input.Time, _wad);
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
            return Last;
        }

        // True Range High and Low
        double trueHigh = Math.Max(high, _prevClose);
        double trueLow = Math.Min(low, _prevClose);

        // Price Movement calculation
        double pm;
        if (close > _prevClose)
        {
            pm = close - trueLow;
        }
        else if (close < _prevClose)
        {
            pm = close - trueHigh;
        }
        else
        {
            pm = 0;
        }

        // A/D value and cumulative WAD
        double ad = pm * volume;
        _wad += ad;

        // Update previous close for next bar
        if (isNew)
        {
            _prevClose = close;
        }

        Last = new TValue(input.Time, _wad);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates WAD with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// WAD requires OHLCV bar data to calculate True Range and Volume.
    /// Use Update(TBar) instead.
    /// </exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException(
            "WAD requires OHLCV bar data to calculate True Range and Volume. " +
            "Use Update(TBar) instead.");
    }

    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
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

    public static TSeries Batch(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.High.Values, source.Low.Values, source.Close.Values, source.Volume.Values, v);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (high.Length != low.Length || high.Length != close.Length || high.Length != volume.Length || high.Length != output.Length)
        {
            throw new ArgumentException("All spans must be of the same length", nameof(output));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // First bar: WAD = 0
        output[0] = 0;
        double prevClose = close[0];
        double wad = 0;

        for (int i = 1; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];
            double vol = volume[i];

            // True Range High and Low
            double trueHigh = Math.Max(h, prevClose);
            double trueLow = Math.Min(l, prevClose);

            // Price Movement
            double pm;
            if (c > prevClose)
            {
                pm = c - trueLow;
            }
            else if (c < prevClose)
            {
                pm = c - trueHigh;
            }
            else
            {
                pm = 0;
            }

            // Accumulate
            wad += pm * vol;
            output[i] = wad;
            prevClose = c;
        }
    }

    public static (TSeries Results, Wad Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Wad();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}