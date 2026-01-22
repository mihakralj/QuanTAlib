// MIDPOINT: Rolling Midpoint - (Highest + Lowest) / 2 over lookback window
// Composes Highest and Lowest indicators for efficient calculation

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MIDPOINT: Rolling Midpoint
/// Calculates the midpoint ((highest + lowest) / 2) over a specified lookback period.
/// Composes Highest and Lowest indicators internally.
/// </summary>
/// <remarks>
/// Key properties:
/// - Returns the center of the price range within the lookback window
/// - Useful for mean reversion, channel center, trend direction
/// - Can be validated against TA-Lib MIDPOINT function
/// </remarks>
[SkipLocalsInit]
public sealed class Midpoint : AbstractBase
{
    private readonly Highest _highest;
    private readonly Lowest _lowest;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _handler;

    public override bool IsHot => _highest.IsHot && _lowest.IsHot;

    /// <summary>
    /// Initializes a new Midpoint indicator with specified lookback period.
    /// </summary>
    /// <param name="period">Lookback window size (must be >= 1)</param>
    public Midpoint(int period)
    {
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        _highest = new Highest(period);
        _lowest = new Lowest(period);
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
        _source = source;
        _handler = HandleUpdate;
        _source.Pub += _handler;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _source != null && _handler != null)
        {
            _source.Pub -= _handler;
        }
        base.Dispose(disposing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        TValue high = _highest.Update(input, isNew);
        TValue low = _lowest.Update(input, isNew);

        double result = (high.Value + low.Value) * 0.5;

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

    public static TSeries Calculate(TSeries source, int period)
    {
        var indicator = new Midpoint(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates rolling midpoint over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        int len = source.Length;

        // Use ArrayPool for large arrays to avoid stack overflow
        double[]? rentedHigh = null;
        double[]? rentedLow = null;

#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
        Span<double> highBuffer = len <= 256
            ? stackalloc double[len]
            : (rentedHigh = System.Buffers.ArrayPool<double>.Shared.Rent(len)).AsSpan(0, len);

        Span<double> lowBuffer = len <= 256
            ? stackalloc double[len]
            : (rentedLow = System.Buffers.ArrayPool<double>.Shared.Rent(len)).AsSpan(0, len);
#pragma warning restore S1121

        try
        {
            Highest.Calculate(source, highBuffer, period);
            Lowest.Calculate(source, lowBuffer, period);

            for (int i = 0; i < len; i++)
            {
                output[i] = (highBuffer[i] + lowBuffer[i]) * 0.5;
            }
        }
        finally
        {
            if (rentedHigh != null)
                System.Buffers.ArrayPool<double>.Shared.Return(rentedHigh);
            if (rentedLow != null)
                System.Buffers.ArrayPool<double>.Shared.Return(rentedLow);
        }
    }

    public override void Reset()
    {
        _highest.Reset();
        _lowest.Reset();
        Last = default;
    }
}
