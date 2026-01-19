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

        int len = source.Length;

        // Use monotonic deque algorithm - allocate on heap for large periods to avoid stack overflow
        int[]? rentedDeque = null;
        double[]? rentedValues = null;

#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
        Span<int> deque = period <= 256
            ? stackalloc int[period]
            : (rentedDeque = System.Buffers.ArrayPool<int>.Shared.Rent(period)).AsSpan(0, period);

        // Separate buffer for corrected values (handles NaN/Infinity)
        Span<double> values = len <= 256
            ? stackalloc double[len]
            : (rentedValues = System.Buffers.ArrayPool<double>.Shared.Rent(len)).AsSpan(0, len);
#pragma warning restore S1121

        try
        {
            // First pass: store corrected values in separate buffer to handle non-finite inputs
            double lastValid = 0.0;
            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                    values[i] = val;
                }
                else
                {
                    values[i] = lastValid;
                }
            }

            // Second pass: compute rolling min using corrected values
            int dequeStart = 0;
            int dequeEnd = 0;
            for (int i = 0; i < len; i++)
            {
                double value = values[i];

                // Remove indices outside window
                while (dequeEnd > dequeStart && deque[dequeStart] <= i - period)
                    dequeStart++;

                // Remove larger values from back (use values[] for corrected values)
                while (dequeEnd > dequeStart && values[deque[dequeEnd - 1]] >= value)
                    dequeEnd--;

                // Compact deque if needed
                if (dequeEnd >= deque.Length)
                {
                    int count = dequeEnd - dequeStart;
                    for (int j = 0; j < count; j++)
                        deque[j] = deque[dequeStart + j];
                    dequeStart = 0;
                    dequeEnd = count;
                }

                deque[dequeEnd++] = i;
                output[i] = values[deque[dequeStart]];
            }
        }
        finally
        {
            if (rentedDeque != null)
                System.Buffers.ArrayPool<int>.Shared.Return(rentedDeque);
            if (rentedValues != null)
                System.Buffers.ArrayPool<double>.Shared.Return(rentedValues);
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