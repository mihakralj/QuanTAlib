using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MEDPRICE: Median Price
/// Calculates the midpoint of High and Low prices.
/// Equivalent to TBar.HL2 but as a proper streaming indicator with bar correction.
/// </summary>
/// <remarks>
/// <b>Calculation:</b>
/// <list type="number">
/// <item>MedPrice = (High + Low) / 2</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Stateless bar-by-bar calculation (no lookback period)</item>
/// <item>TA-Lib compatible (MEDPRICE function)</item>
/// <item>Always hot after first bar</item>
/// <item>Common proxy for "fair value" within a bar</item>
/// </list>
/// </remarks>
[SkipLocalsInit]
public sealed class Medprice : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValidHigh,
        double LastValidLow,
        double LastResult,
        int Count
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Medprice class.
    /// </summary>
    public Medprice()
    {
        WarmupPeriod = 1;
        Name = "Medprice";
        _s = new State(0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Medprice class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    public Medprice(ITValuePublisher source) : this()
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// Computes the median price from High and Low values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeMedianPrice(double high, double low)
    {
        return (high + low) * 0.5;
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// For TValue input, treats the value as both High and Low (result = value).
    /// Prefer Update(TBar) for standard OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a new bar (preferred method).
    /// </summary>
    /// <param name="bar">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated Median Price value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.High, bar.Low, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the Median Price values.</returns>
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

        Batch(source.HighValues, source.LowValues, vSpan);

        for (int i = 0; i < len; i++)
        {
            tSpan[i] = source[i].Time;
        }

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
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
        var values = source.Values;

        // TValue-only: result = value (identity)
        for (int i = 0; i < len; i++)
        {
            tSpan[i] = source.Times[i];
            vSpan[i] = values[i];
        }

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double high, double low, bool isNew)
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
        if (!double.IsFinite(high)) { high = s.LastValidHigh; } else { s.LastValidHigh = high; }
        if (!double.IsFinite(low)) { low = s.LastValidLow; } else { s.LastValidLow = low; }

        double result = ComputeMedianPrice(high, low);

        if (!double.IsFinite(result))
        {
            result = s.LastResult;
        }
        else
        {
            s.LastResult = result;
        }

        if (isNew) { s.Count++; }

        _s = s;

        Last = new TValue(timeTicks, result);
        PubEvent(Last, isNew);
        return Last;
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
        _s = new State(0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates Median Price for a bar series (static).
    /// </summary>
    public static TSeries Batch(TBarSeries source)
    {
        var indicator = new Medprice();
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans for High/Low data.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> output)
    {
        int len = high.Length;
        if (low.Length != len)
        {
            throw new ArgumentException("All input spans must have the same length", nameof(low));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input spans", nameof(output));
        }

        for (int i = 0; i < len; i++)
        {
            output[i] = ComputeMedianPrice(high[i], low[i]);
        }
    }

    /// <summary>
    /// Batch calculation using a TBarSeries (convenience overload).
    /// </summary>
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

        Batch(source.HighValues, source.LowValues, output);
    }

    public static (TSeries Results, Medprice Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Medprice();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
