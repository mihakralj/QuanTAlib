using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HA: Heikin-Ashi
/// Transforms standard OHLC bars into smoothed Heikin-Ashi candles.
/// </summary>
/// <remarks>
/// <b>Calculation:</b>
/// <list type="number">
/// <item>HA_Close = (O + H + L + C) / 4</item>
/// <item>HA_Open = (prev_HA_Open + prev_HA_Close) / 2</item>
/// <item>HA_High = max(H, HA_Open, HA_Close)</item>
/// <item>HA_Low = min(L, HA_Open, HA_Close)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Output is TBar (smoothed OHLC), not TValue</item>
/// <item>HA_Open is a recursive IIR filter (alpha=0.5, half-life=1 bar)</item>
/// <item>HA_Close is stateless OHLC4 (identical to AVGPRICE)</item>
/// <item>Always hot after first bar</item>
/// </list>
/// </remarks>
/// <seealso href="Ha.md">Detailed documentation</seealso>
/// <seealso href="ha.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Ha : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevHaOpen,
        double PrevHaClose,
        double LastValidOpen,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        int Count
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// The last computed Heikin-Ashi bar (full OHLC output).
    /// </summary>
    public TBar LastBar { get; private set; }

    /// <summary>
    /// Initializes a new instance of the Ha class.
    /// </summary>
    public Ha()
    {
        WarmupPeriod = 1;
        Name = "Ha";
        _s = default;
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Ha class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    public Ha(ITValuePublisher source) : this()
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// Computes HA_Close = (O+H+L+C)/4 via FMA.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeHaClose(double open, double high, double low, double close)
    {
        return Math.FusedMultiplyAdd(open + high, 0.25, (low + close) * 0.25);
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// For TValue input, treats value as all four OHLC prices.
    /// Prefer Update(TBar) for standard OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _ = UpdateBar(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// Returns a TBarSeries containing the Heikin-Ashi bars.
    /// </summary>
    public TBarSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TBarSeries();
        }

        int len = source.Count;
        var result = new TBarSeries();

        for (int i = 0; i < len; i++)
        {
            TBar haBar = UpdateBar(source[i], isNew: true);
            result.Add(haBar);
        }

        return result;
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        for (int i = 0; i < len; i++)
        {
            TValue result = Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
            tSpan[i] = result.Time;
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates the indicator with a new bar (preferred method).
    /// Returns the smoothed Heikin-Ashi TBar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar UpdateBar(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, isNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TBar UpdateCore(long timeTicks, double open, double high, double low, double close, double volume, bool isNew)
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

        // Handle non-finite values — use last valid values
        if (!double.IsFinite(open)) { open = s.LastValidOpen; } else { s.LastValidOpen = open; }
        if (!double.IsFinite(high)) { high = s.LastValidHigh; } else { s.LastValidHigh = high; }
        if (!double.IsFinite(low)) { low = s.LastValidLow; } else { s.LastValidLow = low; }
        if (!double.IsFinite(close)) { close = s.LastValidClose; } else { s.LastValidClose = close; }

        // HA Close = OHLC4
        double haClose = ComputeHaClose(open, high, low, close);

        // HA Open = recursive IIR
        double haOpen;
        if (s.Count == 0)
        {
            // Seed: midpoint of O and C
            haOpen = (open + close) * 0.5;
        }
        else
        {
            haOpen = (s.PrevHaOpen + s.PrevHaClose) * 0.5;
        }

        // HA High = max(H, haOpen, haClose)
        double haHigh = Math.Max(high, Math.Max(haOpen, haClose));

        // HA Low = min(L, haOpen, haClose)
        double haLow = Math.Min(low, Math.Min(haOpen, haClose));

        // Store state for next bar
        s.PrevHaOpen = haOpen;
        s.PrevHaClose = haClose;

        if (isNew) { s.Count++; }

        _s = s;

        LastBar = new TBar(timeTicks, haOpen, haHigh, haLow, haClose, volume);
        Last = new TValue(timeTicks, haClose);
        PubEvent(Last, isNew);
        return LastBar;
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow.Ticks, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _s = default;
        _ps = _s;
        Last = default;
        LastBar = default;
    }

    /// <summary>
    /// Calculates Heikin-Ashi bars for a bar series (static).
    /// </summary>
    public static TBarSeries Batch(TBarSeries source)
    {
        var indicator = new Ha();
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation using OHLC spans. Outputs 4 spans for HA O, H, L, C.
    /// HA_Open is sequential (IIR), so this cannot be fully vectorized.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> haOpenOut,
        Span<double> haHighOut,
        Span<double> haLowOut,
        Span<double> haCloseOut)
    {
        int len = open.Length;
        if (high.Length != len || low.Length != len || close.Length != len)
        {
            throw new ArgumentException("All input spans must have the same length", nameof(high));
        }
        if (haOpenOut.Length < len || haHighOut.Length < len || haLowOut.Length < len || haCloseOut.Length < len)
        {
            throw new ArgumentException("All output spans must be at least as long as input spans", nameof(haOpenOut));
        }

        if (len == 0) { return; }

        // First bar: seed
        double hc = ComputeHaClose(open[0], high[0], low[0], close[0]);
        double ho = (open[0] + close[0]) * 0.5;
        haCloseOut[0] = hc;
        haOpenOut[0] = ho;
        haHighOut[0] = Math.Max(high[0], Math.Max(ho, hc));
        haLowOut[0] = Math.Min(low[0], Math.Min(ho, hc));

        double prevHaOpen = ho;
        double prevHaClose = hc;

        // Sequential pass (IIR dependency on HA_Open)
        for (int i = 1; i < len; i++)
        {
            hc = ComputeHaClose(open[i], high[i], low[i], close[i]);
            ho = (prevHaOpen + prevHaClose) * 0.5;

            haCloseOut[i] = hc;
            haOpenOut[i] = ho;
            haHighOut[i] = Math.Max(high[i], Math.Max(ho, hc));
            haLowOut[i] = Math.Min(low[i], Math.Min(ho, hc));

            prevHaOpen = ho;
            prevHaClose = hc;
        }
    }

    /// <summary>
    /// Static Calculate returning both results and indicator state.
    /// </summary>
    public static (TBarSeries Results, Ha Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Ha();
        TBarSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
