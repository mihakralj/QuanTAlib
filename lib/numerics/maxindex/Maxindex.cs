using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MAXINDEX: Rolling Maximum Index
/// Returns the position of the maximum value within a rolling window.
/// Streaming mode: bars-ago offset (0=current, period-1=oldest).
/// Batch span mode: absolute array index (TA-Lib compatible).
/// </summary>
/// <remarks>
/// Key properties:
/// - Returns the index/position of the highest value, not the value itself
/// - Streaming output is "bars-ago" offset for natural streaming consumption
/// - Batch(ReadOnlySpan) output is absolute array index matching TA-Lib MAXINDEX
/// - Tie-breaking: last occurrence wins (most recent bar, using >= comparison)
/// - Can be cross-validated: source[Maxindex.Batch[i]] == Highest.Batch[i]
/// </remarks>
[SkipLocalsInit]
public sealed class Maxindex : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a new Maxindex indicator with specified lookback period.
    /// </summary>
    /// <param name="period">Lookback window size (must be >= 2)</param>
    public Maxindex(int period)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Maxindex({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Initializes a new Maxindex indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback window size</param>
    public Maxindex(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double value = double.IsFinite(input.Value) ? input.Value : _state.LastValid;
        _state = new State(value);

        _buffer.Add(value, isNew);

        // Scan the ring buffer to find the bars-ago index of the maximum value.
        // Tie-breaking: >= means last occurrence (most recent) wins.
        ReadOnlySpan<double> span = _buffer.GetSpan();
        int len = span.Length;
        double maxVal = span[0];
        int maxPos = 0;
        for (int i = 1; i < len; i++)
        {
            if (span[i] >= maxVal)
            {
                maxVal = span[i];
                maxPos = i;
            }
        }

        // Convert to bars-ago: newest element is at index (len - 1), oldest at 0.
        // bars-ago = (len - 1) - maxPos
        double result = (len - 1) - maxPos;

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
            Update(input: new TValue(time, source[i]), isNew: true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Maxindex(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates rolling maximum index over a span of values.
    /// Output contains ABSOLUTE array indices (TA-Lib MAXINDEX compatible).
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

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        int len = source.Length;

        // Use monotonic deque algorithm — same as Highest but output index, not value.
        int[]? rentedDeque = null;
        double[]? rentedValues = null;

#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
        Span<int> deque = period <= 256
            ? stackalloc int[period]
            : (rentedDeque = System.Buffers.ArrayPool<int>.Shared.Rent(period)).AsSpan(0, period);

        Span<double> values = len <= 256
            ? stackalloc double[len]
            : (rentedValues = System.Buffers.ArrayPool<double>.Shared.Rent(len)).AsSpan(0, len);
#pragma warning restore S1121

        try
        {
            // First pass: store corrected values (handle NaN/Infinity)
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

            // Second pass: compute rolling max index using monotonic deque.
            // Circular buffer indexing — branch-based wrapping is faster than modulo.
            int head = 0;  // front of deque (oldest/max)
            int tail = 0;  // back of deque (newest)
            int count = 0; // number of elements in deque
            int capacity = deque.Length;

            for (int i = 0; i < len; i++)
            {
                double value = values[i];

                // Remove indices outside window from front
                while (count > 0 && deque[head] <= i - period)
                {
                    head++;
                    if (head >= capacity)
                    {
                        head -= capacity;
                    }

                    count--;
                }

                // Remove smaller-or-equal values from back (>= tie-breaking: last occurrence wins)
                while (count > 0)
                {
                    int backIdx = tail - 1;
                    if (backIdx < 0)
                    {
                        backIdx += capacity;
                    }

                    if (values[deque[backIdx]] <= value)
                    {
                        tail = backIdx;
                        count--;
                    }
                    else
                    {
                        break;
                    }
                }

                // Add current index at tail
                deque[tail] = i;
                tail++;
                if (tail >= capacity)
                {
                    tail -= capacity;
                }

                count++;

                // Output the ABSOLUTE index of the maximum (not the value)
                output[i] = deque[head];
            }
        }
        finally
        {
            if (rentedDeque != null)
            {
                System.Buffers.ArrayPool<int>.Shared.Return(rentedDeque);
            }

            if (rentedValues != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedValues);
            }
        }
    }

    public static (TSeries Results, Maxindex Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Maxindex(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}
