// LOWEST: Rolling Minimum - Minimum value over lookback window
// Uses RingBuffer's SIMD-accelerated Min() for efficient computation

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// LOWEST: Rolling Minimum
/// Calculates the minimum value over a specified lookback period.
/// Uses RingBuffer's SIMD-accelerated Min() method.
/// </summary>
/// <remarks>
/// Key properties:
/// - Returns the lowest value within the lookback window
/// - Useful for support levels, drawdown detection, normalization
/// - Can be validated against TA-Lib MIN function
/// </remarks>
[SkipLocalsInit]
public sealed class Lowest : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <param name="period">Lookback window size (must be >= 1)</param>
    public Lowest(int period)
    {
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Lowest({period})";
        WarmupPeriod = period;
    }

    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback window size</param>
    public Lowest(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
            _p_state = _state;
        else
            _state = _p_state;

        double value = double.IsFinite(input.Value) ? input.Value : _state.LastValid;
        _state = new State(value);

        _buffer.Add(value, isNew);

        double result = _buffer.Min();

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
        var indicator = new Lowest(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates rolling minimum over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        // Use monotonic deque algorithm
        Span<int> deque = stackalloc int[Math.Min(period, 256)];
        int dequeStart = 0;
        int dequeEnd = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double fallback = i > 0 ? output[i - 1] : 0.0;
            double value = double.IsFinite(source[i]) ? source[i] : fallback;

            // Remove indices outside window
            while (dequeEnd > dequeStart && deque[dequeStart] <= i - period)
                dequeStart++;

            // Remove larger values from back (monotonic increasing for min)
            while (dequeEnd > dequeStart && source[deque[dequeEnd - 1]] >= value)
                dequeEnd--;

            // Wrap around if needed for large periods
            if (dequeEnd >= deque.Length)
            {
                // Compact deque
                int count = dequeEnd - dequeStart;
                for (int j = 0; j < count; j++)
                    deque[j] = deque[dequeStart + j];
                dequeStart = 0;
                dequeEnd = count;
            }

            deque[dequeEnd++] = i;
            output[i] = source[deque[dequeStart]];
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}
