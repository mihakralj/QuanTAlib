// MIDPOINT: Rolling Midpoint - (Highest + Lowest) / 2 over lookback window
// Uses RingBuffer directly for self-contained core dependency (no Highest/Lowest composition)

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MIDPOINT: Rolling Midpoint
/// Calculates the midpoint ((highest + lowest) / 2) over a specified lookback period.
/// Uses RingBuffer directly for O(N) max/min scanning per update.
/// </summary>
/// <remarks>
/// Key properties:
/// - Returns the center of the value range within the lookback window
/// - Useful for mean reversion, channel center, trend direction
/// - Can be validated against TA-Lib MIDPOINT function
/// - Self-contained: uses RingBuffer directly (no Highest/Lowest dependency)
/// </remarks>
[SkipLocalsInit]
public sealed class Midpoint : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _s, _ps;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Midpoint indicator with specified lookback period.
    /// </summary>
    /// <param name="period">Lookback window size (must be >= 1)</param>
    public Midpoint(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Midpoint({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Initializes a new Midpoint indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback window size</param>
    public Midpoint(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
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

        double value = double.IsFinite(input.Value) ? input.Value : s.LastValid;
        s = new State(value);

        _buffer.Add(value, isNew);

        double result = (_buffer.Max() + _buffer.Min()) * 0.5;

        _s = s;
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
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

    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Midpoint(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates rolling midpoint over a span of values.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        int len = source.Length;
        var buf = new RingBuffer(period);

        for (int i = 0; i < len; i++)
        {
            double fallback = i > 0 ? output[i - 1] : 0;
            double v = double.IsFinite(source[i]) ? source[i] : fallback;
            buf.Add(v, true);
            output[i] = (buf.Max() + buf.Min()) * 0.5;
        }
    }

    public static (TSeries Results, Midpoint Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Midpoint(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _s = default;
        _ps = default;
        Last = default;
    }
}
