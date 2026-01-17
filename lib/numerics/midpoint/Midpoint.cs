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

    public override bool IsHot => _highest.IsHot && _lowest.IsHot;

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

    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback window size</param>
    public Midpoint(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += HandleUpdate;
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

        // Calculate highest and lowest, then combine
        Span<double> highBuffer = stackalloc double[source.Length];
        Span<double> lowBuffer = stackalloc double[source.Length];

        Highest.Calculate(source, highBuffer, period);
        Lowest.Calculate(source, lowBuffer, period);

        for (int i = 0; i < source.Length; i++)
        {
            output[i] = (highBuffer[i] + lowBuffer[i]) * 0.5;
        }
    }

    public override void Reset()
    {
        _highest.Reset();
        _lowest.Reset();
        Last = default;
    }
}
