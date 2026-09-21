using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MIDPRICE: Midpoint Price over Period
/// Calculates the midpoint of the highest High and lowest Low over a rolling window.
/// Unlike Midpoint (which operates on a single series), Midprice uses separate H/L channels.
/// </summary>
/// <remarks>
/// <b>Calculation:</b>
/// <list type="number">
/// <item>MidPrice = (Highest(High, N) + Lowest(Low, N)) / 2</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Rolling bar-level calculation with lookback period</item>
/// <item>TA-Lib compatible (MIDPRICE function)</item>
/// <item>Uses RingBuffer directly for self-contained core dependency</item>
/// <item>Represents the center of the price channel over the lookback window</item>
/// </list>
///
/// <b>Difference from Midpoint:</b>
/// <list type="bullet">
/// <item>Midpoint operates on a single value series: (Highest(V,N) + Lowest(V,N)) / 2</item>
/// <item>Midprice operates on OHLC bars: (Highest(H,N) + Lowest(L,N)) / 2</item>
/// </list>
/// </remarks>
[SkipLocalsInit]
public sealed class Midprice : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _highBuffer;
    private readonly RingBuffer _lowBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidHigh, double LastValidLow);
    private State _s, _ps;

    /// <summary>
    /// True if both internal buffers have enough data for valid results.
    /// </summary>
    public override bool IsHot => _highBuffer.Count >= _period;

    /// <summary>
    /// Initializes a new instance of the Midprice class.
    /// </summary>
    /// <param name="period">Lookback window size (must be >= 1)</param>
    public Midprice(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _highBuffer = new RingBuffer(period);
        _lowBuffer = new RingBuffer(period);
        Name = $"Midprice({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Initializes a new instance of the Midprice class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">Lookback window size.</param>
    public Midprice(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// For TValue input, treats the value as both High and Low (same as Midpoint behavior).
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
    /// <returns>The calculated Midprice value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.High, bar.Low, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the Midprice values.</returns>
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

        Batch(source.HighValues, source.LowValues, vSpan, WarmupPeriod);

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
    public override TSeries Update(TSeries source)
    {
        var result = new TSeries(source.Count);
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), values[i]), true);
            result.Add(tv, true);
        }
        return result;
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

        double h = double.IsFinite(high) ? high : s.LastValidHigh;
        double l = double.IsFinite(low) ? low : s.LastValidLow;
        s = new State(h, l);

        _highBuffer.Add(h, isNew);
        _lowBuffer.Add(l, isNew);

        double result = (_highBuffer.Max() + _lowBuffer.Min()) * 0.5;

        _s = s;
        Last = new TValue(timeTicks, result);
        PubEvent(Last, isNew);
        return Last;
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }
    public override void Reset()
    {
        _highBuffer.Clear();
        _lowBuffer.Clear();
        _s = default;
        _ps = default;
        Last = default;
    }

    /// <summary>
    /// Calculates Midprice for a bar series (static).
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period)
    {
        var indicator = new Midprice(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans for High/Low data with rolling window.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> output,
        int period)
    {
        int len = high.Length;
        if (low.Length != len)
        {
            throw new ArgumentException("High and Low spans must have the same length", nameof(low));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input spans", nameof(output));
        }
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        // Use RingBuffer for rolling max/min — self-contained, no Highest/Lowest dependency
        var highBuf = new RingBuffer(period);
        var lowBuf = new RingBuffer(period);

        for (int i = 0; i < len; i++)
        {
            double fallback = i > 0 ? output[i - 1] : 0;
            double h = double.IsFinite(high[i]) ? high[i] : fallback;
            double l = double.IsFinite(low[i]) ? low[i] : fallback;

            highBuf.Add(h, true);
            lowBuf.Add(l, true);

            output[i] = (highBuf.Max() + lowBuf.Min()) * 0.5;
        }
    }

    /// <summary>
    /// Batch calculation using a TBarSeries (convenience overload).
    /// </summary>
    public static void Batch(TBarSeries source, Span<double> output, int period)
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

        Batch(source.HighValues, source.LowValues, output, period);
    }

    public static (TSeries Results, Midprice Indicator) Calculate(TBarSeries source, int period)
    {
        var indicator = new Midprice(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
