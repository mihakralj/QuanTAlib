// True Range (TR) Indicator
// Measures the maximum price movement including gaps from the previous close

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TR: True Range
/// A volatility measure that captures the maximum price movement including gaps.
/// True Range accounts for overnight gaps by comparing current High-Low range
/// against the previous close.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate three ranges: (High - Low), |High - prevClose|, |Low - prevClose|</item>
/// <item>True Range = max(all three ranges)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Bar-by-bar calculation (no smoothing)</item>
/// <item>Always positive (absolute values used for gap calculations)</item>
/// <item>First bar uses High - Low (no previous close available)</item>
/// <item>Foundation for ATR (Average True Range)</item>
/// </list>
///
/// <b>Sources:</b>
/// J. Welles Wilder Jr. (1978). "New Concepts in Technical Trading Systems"
/// </remarks>
[SkipLocalsInit]
public sealed class Tr : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevClose,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        double LastTr,
        int Count
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Tr class.
    /// </summary>
    public Tr()
    {
        WarmupPeriod = 1;
        Name = "Tr";
        _s = new State(double.NaN, 0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Tr class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    public Tr(ITValuePublisher source) : this()
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// Computes the True Range for given bar values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeTrueRange(double high, double low, double prevClose)
    {
        double tr1 = high - low;
        double tr2 = Math.Abs(high - prevClose);
        double tr3 = Math.Abs(low - prevClose);
        return Math.Max(tr1, Math.Max(tr2, tr3));
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// For TR, this treats the value as a close price (uses value for H, L, and C).
    /// Prefer Update(TBar) for standard OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // For TValue input, treat it as if H=L=C (no range)
        return UpdateCore(input.Time, input.Value, input.Value, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a new bar (preferred method).
    /// </summary>
    /// <param name="bar">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated True Range value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.High, bar.Low, bar.Close, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the True Range values.</returns>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        // Extract OHLC data
        Span<double> highs = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> lows = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> closes = len <= 128 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            highs[i] = source[i].High;
            lows[i] = source[i].Low;
            closes[i] = source[i].Close;
            tSpan[i] = source[i].Time;
        }

        Batch(highs, lows, closes, vSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
    }
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var values = source.Values;

        // When using TSeries (close prices only), TR = |close[i] - close[i-1]| (gap-based)
        // This is a degenerate case - prefer TBarSeries
        for (int i = 0; i < len; i++)
        {
            tSpan[i] = source.Times[i];
            vSpan[i] = (i == 0) ? 0 : Math.Abs(values[i] - values[i - 1]);
        }

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double high, double low, double close, bool isNew)
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

        // Handle non-finite values - use last valid values
        if (!double.IsFinite(high))
        {
            high = s.LastValidHigh;
        }
        else
        {
            s.LastValidHigh = high;
        }

        if (!double.IsFinite(low))
        {
            low = s.LastValidLow;
        }
        else
        {
            s.LastValidLow = low;
        }

        if (!double.IsFinite(close))
        {
            close = s.LastValidClose;
        }
        else
        {
            s.LastValidClose = close;
        }

        double tr;
        if (s.Count == 0 || !double.IsFinite(s.PrevClose))
        {
            // First bar or no previous close: use High - Low
            tr = high - low;
        }
        else
        {
            tr = ComputeTrueRange(high, low, s.PrevClose);
        }

        if (!double.IsFinite(tr) || tr < 0)
        {
            tr = s.LastTr;
        }
        else
        {
            s.LastTr = tr;
        }

        // Update state
        s.PrevClose = close;
        if (isNew)
        {
            s.Count++;
        }

        _s = s;

        Last = new TValue(timeTicks, tr);
        PubEvent(Last, isNew);
        return Last;
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
        _s = new State(double.NaN, 0, 0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates True Range for a bar series (static).
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the True Range values.</returns>
    public static TSeries Batch(TBarSeries source)
    {
        var tr = new Tr();
        return tr.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans for OHLC data.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output True Range values.</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output)
    {
        int len = high.Length;
        if (low.Length != len || close.Length != len)
        {
            throw new ArgumentException("All input spans must have the same length", nameof(low));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input spans", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        double lastValidHigh = 0;
        double lastValidLow = 0;
        double lastValidClose = 0;
        double lastTr = 0;

        for (int i = 0; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];

            // Handle non-finite values
            if (!double.IsFinite(h))
            {
                h = lastValidHigh;
            }
            else
            {
                lastValidHigh = h;
            }

            if (!double.IsFinite(l))
            {
                l = lastValidLow;
            }
            else
            {
                lastValidLow = l;
            }

            double tr;
            if (i == 0)
            {
                // First bar: use High - Low
                tr = h - l;
            }
            else
            {
                double prevClose = close[i - 1];
                if (!double.IsFinite(prevClose))
                {
                    // Fall back to last valid close from previous bars
                    prevClose = lastValidClose;
                }
                tr = ComputeTrueRange(h, l, prevClose);
            }

            // Update lastValidClose AFTER computing TR so fallback uses previous bar's close
            if (double.IsFinite(c))
            {
                lastValidClose = c;
            }

            if (!double.IsFinite(tr) || tr < 0)
            {
                tr = lastTr;
            }
            else
            {
                lastTr = tr;
            }

            output[i] = tr;
        }
    }

    /// <summary>
    /// Batch calculation using a TBarSeries (convenience overload).
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <param name="output">Output True Range values.</param>
    public static void Batch(TBarSeries source, Span<double> output)
    {
        int len = source.Count;
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as source", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        Span<double> highs = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> lows = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> closes = len <= 128 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            highs[i] = source[i].High;
            lows[i] = source[i].Low;
            closes[i] = source[i].Close;
        }

        Batch(highs, lows, closes, output);
    }

    public static (TSeries Results, Tr Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Tr();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

}
