// Volatility Ratio (VR) Indicator
// Measures True Range relative to Average True Range to identify volatility breakouts

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VR: Volatility Ratio
/// Calculates the ratio of True Range to Average True Range.
/// Values above 1.0 indicate higher-than-average volatility; below 1.0 indicates lower.
/// Uses bias-corrected RMA for ATR calculation.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate True Range: max(H-L, |H-PrevClose|, |L-PrevClose|)</item>
/// <item>Calculate ATR using bias-corrected RMA</item>
/// <item>VR = TR / ATR</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Values greater than 1.0 indicate current volatility exceeds average</item>
/// <item>Values less than 1.0 indicate current volatility below average</item>
/// <item>Useful for breakout detection and volatility regime changes</item>
/// <item>Bias-corrected RMA provides accurate results during warmup</item>
/// </list>
/// </remarks>
[SkipLocalsInit]
public sealed class Vr : AbstractBase
{
    private readonly int _period;
    private const double Epsilon = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double RawAtr,
        double ECompensator,
        double PrevClose,
        double LastValidVr,
        int Count,
        bool HasPrevClose
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Vr class.
    /// </summary>
    /// <param name="period">The ATR lookback period (default 14).</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Vr(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        _period = period;
        WarmupPeriod = period;
        Name = $"Vr({period})";
        _s = new State(0, 1.0, 0, 0, 0, false);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Vr class with a TBarSeries source.
    /// </summary>
    /// <param name="source">The data source for priming.</param>
    /// <param name="period">The ATR lookback period (default 14).</param>
    public Vr(TBarSeries source, int period = 14) : this(period)
    {
        // Prime with historical data
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= _period;

    /// <summary>
    /// The ATR lookback period.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="bar">The input bar (OHLC required).</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated VR value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double high = bar.High;
        double low = bar.Low;
        double close = bar.Close;

        // Handle non-finite values
        if (!double.IsFinite(high) || !double.IsFinite(low) || !double.IsFinite(close))
        {
            Last = new TValue(bar.Time, s.LastValidVr);
            PubEvent(Last, isNew);
            return Last;
        }

        // Calculate True Range
        double tr;
        double hl = high - low;

        if (s.HasPrevClose)
        {
            double hPc = Math.Abs(high - s.PrevClose);
            double lPc = Math.Abs(low - s.PrevClose);
            tr = Math.Max(hl, Math.Max(hPc, lPc));
        }
        else
        {
            tr = hl;
        }

        // Bias-corrected RMA for ATR
        double alpha = 1.0 / _period;
        double rawAtr;
        double eComp;

        if (s.Count == 0)
        {
            // First bar: initialize with TR
            rawAtr = tr;
            eComp = 1.0 - alpha;
        }
        else
        {
            // RMA update: (prev * (period-1) + value) / period
            rawAtr = (s.RawAtr * (_period - 1) + tr) / _period;
            eComp = (1.0 - alpha) * s.ECompensator;
        }

        // Bias correction
        double atr = eComp > Epsilon ? rawAtr / (1.0 - eComp) : rawAtr;

        // Calculate VR = TR / ATR
        double vr = atr > Epsilon ? tr / atr : 0;

        if (!double.IsFinite(vr) || vr < 0)
        {
            vr = s.LastValidVr;
        }
        else
        {
            s.LastValidVr = vr;
        }

        // Update state
        s.RawAtr = rawAtr;
        s.ECompensator = eComp;
        if (isNew)
        {
            s.PrevClose = close;
            s.HasPrevClose = true;
            s.Count = Math.Min(s.Count + 1, _period);
        }

        _s = s;

        Last = new TValue(bar.Time, vr);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a TValue input (uses value as all OHLC).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Create a synthetic bar with the same OHLC (TR will be 0 for single values)
        var bar = new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0);
        return Update(bar, isNew);
    }

    /// <summary>
    /// Updates the indicator with a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        // Use batch calculation
        Batch(source, vSpan, _period);

        for (int i = 0; i < len; i++)
        {
            tSpan[i] = source[i].Time;
        }

        // Update internal state by replaying
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        // For TSeries (price-only), create synthetic bars
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Reset();
        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i], isNew: true);
            t[i] = result.Time;
            v[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _s = new State(0, 1.0, 0, 0, 0, false);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates VR for a TBarSeries (static).
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        var vr = new Vr(period);
        return vr.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    public static void Batch(
        TBarSeries source,
        Span<double> output,
        int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (output.Length < source.Count)
        {
            throw new ArgumentException("Output span must be at least as long as source", nameof(output));
        }

        int len = source.Count;
        if (len == 0)
        {
            return;
        }

        double rawAtr = 0;
        double eComp = 1.0;
        double alpha = 1.0 / period;

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            double high = bar.High;
            double low = bar.Low;
            double close = bar.Close;

            // Previous close (use close for first bar - no gap)
            double prevClose = i > 0 ? source[i - 1].Close : close;

            // Calculate True Range
            double hl = high - low;
            double hPc = i > 0 ? Math.Abs(high - prevClose) : 0;
            double lPc = i > 0 ? Math.Abs(low - prevClose) : 0;
            double tr = i > 0 ? Math.Max(hl, Math.Max(hPc, lPc)) : hl;

            // Bias-corrected RMA
            if (i == 0)
            {
                rawAtr = tr;
                eComp = 1.0 - alpha;
            }
            else
            {
                rawAtr = (rawAtr * (period - 1) + tr) / period;
                eComp = (1.0 - alpha) * eComp;
            }

            double atr = eComp > Epsilon ? rawAtr / (1.0 - eComp) : rawAtr;

            // Calculate VR
            double vr = atr > Epsilon ? tr / atr : 0;

            if (!double.IsFinite(vr) || vr < 0)
            {
                vr = i > 0 ? output[i - 1] : 0;
            }

            output[i] = vr;
        }
    }

    /// <summary>
    /// Batch calculation for OHLC arrays.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        int len = high.Length;
        if (low.Length < len || close.Length < len)
        {
            throw new ArgumentException("All HLC spans must have same length", nameof(low));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        double rawAtr = 0;
        double eComp = 1.0;
        double alpha = 1.0 / period;

        for (int i = 0; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];
            double prevClose = i > 0 ? close[i - 1] : c;

            double hl = h - l;
            double hPc = i > 0 ? Math.Abs(h - prevClose) : 0;
            double lPc = i > 0 ? Math.Abs(l - prevClose) : 0;
            double tr = i > 0 ? Math.Max(hl, Math.Max(hPc, lPc)) : hl;

            if (i == 0)
            {
                rawAtr = tr;
                eComp = 1.0 - alpha;
            }
            else
            {
                rawAtr = (rawAtr * (period - 1) + tr) / period;
                eComp = (1.0 - alpha) * eComp;
            }

            double atr = eComp > Epsilon ? rawAtr / (1.0 - eComp) : rawAtr;
            double vr = atr > Epsilon ? tr / atr : 0;

            if (!double.IsFinite(vr) || vr < 0)
            {
                vr = i > 0 ? output[i - 1] : 0;
            }

            output[i] = vr;
        }
    }

    public static (TSeries Results, Vr Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Vr(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

}
