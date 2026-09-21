// Yang-Zhang Volatility (YZV) Indicator
// A comprehensive volatility measure that combines overnight, open-to-close, and high-low components

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// YZV: Yang-Zhang Volatility
/// A historical volatility estimator that incorporates overnight gaps, open-to-close moves,
/// and high-low ranges using the Rogers-Satchell approach, then smooths with bias-corrected RMA.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate overnight return: ln(Open/PrevClose)</item>
/// <item>Calculate close-to-open return: ln(Close/Open)</item>
/// <item>Calculate Rogers-Satchell component: ln(H/O)*(ln(H/O)-ln(C/O)) + ln(L/O)*(ln(L/O)-ln(C/O))</item>
/// <item>Combine: σ² = ro² + k*rc² + (1-k)*rs² where k = 0.34/(1.34 + (N+1)/(N-1))</item>
/// <item>Smooth using bias-corrected RMA</item>
/// <item>Return sqrt(smoothed variance)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>More efficient than close-to-close estimators</item>
/// <item>Incorporates overnight gap information</item>
/// <item>Uses Rogers-Satchell for intraday volatility</item>
/// <item>Bias-corrected RMA for smoothing during warmup</item>
/// </list>
/// </remarks>
[SkipLocalsInit]
public sealed class Yzv : AbstractBase
{
    private readonly int _period;
    private const double Epsilon = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double RawRma,
        double ECompensator,
        double PrevClose,
        double LastValidYzv,
        int Count,
        bool HasPrevClose
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Yzv class.
    /// </summary>
    /// <param name="period">The lookback period for RMA smoothing (default 20).</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Yzv(int period = 20)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        _period = period;
        WarmupPeriod = period;
        Name = $"Yzv({period})";
        _s = new State(0, 1.0, 0, 0, 0, false);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Yzv class with a TBar source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">The lookback period for RMA smoothing (default 20).</param>
    public Yzv(TBarSeries source, int period = 20) : this(period)
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
    /// The lookback period for RMA smoothing.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="bar">The input bar (OHLC required).</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated YZV value.</returns>
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

        double open = bar.Open;
        double high = bar.High;
        double low = bar.Low;
        double close = bar.Close;

        // Handle non-finite values
        if (!double.IsFinite(open) || !double.IsFinite(high) ||
            !double.IsFinite(low) || !double.IsFinite(close))
        {
            Last = new TValue(bar.Time, s.LastValidYzv);
            PubEvent(Last, isNew);
            return Last;
        }

        // Use previous close or open for first bar
        double prevClose = s.HasPrevClose ? s.PrevClose : open;

        // Calculate log returns
        double ro = Math.Log(open / prevClose);           // Overnight return
        double rc = Math.Log(close / open);               // Close-to-open return
        double rh = Math.Log(high / open);                // High-to-open
        double rl = Math.Log(low / open);                 // Low-to-open

        // Component variances
        double sOSq = ro * ro;                            // Overnight variance
        double sCSq = rc * rc;                            // Close-to-close variance
        double sRsSq = (rh * (rh - rc)) + (rl * (rl - rc));   // Rogers-Satchell variance

        // Yang-Zhang weighting factor
        double ratioN = _period <= 1 ? 1.0 : (double)(_period + 1) / (_period - 1);
        double kYz = 0.34 / (1.34 + ratioN);

        // Combined daily variance
        double sSqDaily = Math.FusedMultiplyAdd(kYz, sCSq, Math.FusedMultiplyAdd(1.0 - kYz, sRsSq, sOSq));

        // Bias-corrected RMA smoothing
        double alpha = 1.0 / _period;
        double rawRma;
        double eComp;

        if (s.Count == 0)
        {
            // First bar: initialize RMA with first value
            rawRma = sSqDaily;
            eComp = 1.0 - alpha;
        }
        else
        {
            // RMA update: (prev * (period-1) + value) / period
            rawRma = ((s.RawRma * (_period - 1)) + sSqDaily) / _period;
            eComp = (1.0 - alpha) * s.ECompensator;
        }

        // Bias correction
        double smoothedSSq = eComp > Epsilon ? rawRma / (1.0 - eComp) : rawRma;

        // Calculate YZV as sqrt of smoothed variance
        double yzv = Math.Sqrt(Math.Max(0.0, smoothedSSq));

        if (!double.IsFinite(yzv) || yzv < 0)
        {
            yzv = s.LastValidYzv;
        }
        else
        {
            s.LastValidYzv = yzv;
        }

        // Update state
        s.RawRma = rawRma;
        s.ECompensator = eComp;
        if (isNew)
        {
            s.PrevClose = close;
            s.HasPrevClose = true;
            s.Count = Math.Min(s.Count + 1, _period);
        }

        _s = s;

        Last = new TValue(bar.Time, yzv);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a TValue input (uses value as close, assumes no gaps).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Create a synthetic bar with the same OHLC
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
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _s = new State(0, 1.0, 0, 0, 0, false);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates YZV for a TBarSeries (static).
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 20)
    {
        var yzv = new Yzv(period);
        return yzv.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    public static void Batch(
        TBarSeries source,
        Span<double> output,
        int period = 20)
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

        double rawRma = 0;
        double eComp = 1.0;
        double alpha = 1.0 / period;
        double ratioN = period <= 1 ? 1.0 : (double)(period + 1) / (period - 1);
        double kYz = 0.34 / (1.34 + ratioN);

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            double open = bar.Open;
            double high = bar.High;
            double low = bar.Low;
            double close = bar.Close;

            // Previous close (use open for first bar)
            double prevClose = i > 0 ? source[i - 1].Close : open;

            // Calculate log returns
            double ro = Math.Log(open / prevClose);
            double rc = Math.Log(close / open);
            double rh = Math.Log(high / open);
            double rl = Math.Log(low / open);

            // Component variances
            double sOSq = ro * ro;
            double sCSq = rc * rc;
            double sRsSq = (rh * (rh - rc)) + (rl * (rl - rc));

            // Combined daily variance
            double sSqDaily = Math.FusedMultiplyAdd(kYz, sCSq, Math.FusedMultiplyAdd(1.0 - kYz, sRsSq, sOSq));

            // Bias-corrected RMA
            if (i == 0)
            {
                rawRma = sSqDaily;
                eComp = 1.0 - alpha;
            }
            else
            {
                rawRma = ((rawRma * (period - 1)) + sSqDaily) / period;
                eComp = (1.0 - alpha) * eComp;
            }

            double smoothedSSq = eComp > Epsilon ? rawRma / (1.0 - eComp) : rawRma;
            double yzv = Math.Sqrt(Math.Max(0.0, smoothedSSq));

            if (!double.IsFinite(yzv) || yzv < 0)
            {
                yzv = i > 0 ? output[i - 1] : 0;
            }

            output[i] = yzv;
        }
    }

    /// <summary>
    /// Batch calculation for OHLC arrays.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 20)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        int len = open.Length;
        if (high.Length < len || low.Length < len || close.Length < len)
        {
            throw new ArgumentException("All OHLC spans must have same length", nameof(high));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        double rawRma = 0;
        double eComp = 1.0;
        double alpha = 1.0 / period;
        double ratioN = period <= 1 ? 1.0 : (double)(period + 1) / (period - 1);
        double kYz = 0.34 / (1.34 + ratioN);

        for (int i = 0; i < len; i++)
        {
            double o = open[i];
            double h = high[i];
            double l = low[i];
            double c = close[i];
            double prevClose = i > 0 ? close[i - 1] : o;

            double ro = Math.Log(o / prevClose);
            double rc = Math.Log(c / o);
            double rh = Math.Log(h / o);
            double rl = Math.Log(l / o);

            double sOSq = ro * ro;
            double sCSq = rc * rc;
            double sRsSq = (rh * (rh - rc)) + (rl * (rl - rc));

            double sSqDaily = Math.FusedMultiplyAdd(kYz, sCSq, Math.FusedMultiplyAdd(1.0 - kYz, sRsSq, sOSq));

            if (i == 0)
            {
                rawRma = sSqDaily;
                eComp = 1.0 - alpha;
            }
            else
            {
                rawRma = ((rawRma * (period - 1)) + sSqDaily) / period;
                eComp = (1.0 - alpha) * eComp;
            }

            double smoothedSSq = eComp > Epsilon ? rawRma / (1.0 - eComp) : rawRma;
            double yzv = Math.Sqrt(Math.Max(0.0, smoothedSSq));

            if (!double.IsFinite(yzv) || yzv < 0)
            {
                yzv = i > 0 ? output[i - 1] : 0;
            }

            output[i] = yzv;
        }
    }

    public static (TSeries Results, Yzv Indicator) Calculate(TBarSeries source, int period = 20)
    {
        var indicator = new Yzv(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
