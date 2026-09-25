// LAG: Value Delay
// Republishes the input value from n bars ago: result = x[t-n].

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// LAG: Value Delay
/// Republishes the input value observed n bars ago.
/// </summary>
/// <remarks>
/// Key properties:
/// - LAG(0) is the identity transform (republishes the current value)
/// - During warmup (fewer than n+1 bars observed), publishes 0.0, consistent with the
///   library's other fixed-lookback transforms (see Mom)
/// - NaN/Infinity inputs are replaced by the last valid value before entering the window
/// - Backing store is a RingBuffer(n+1), so IsHot becomes true once n+1 bars have been seen
/// - Feeds conditions such as <c>close &gt; close[5]</c> once combined with a comparison node
/// </remarks>
/// <seealso href="Lag.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Lag : AbstractBase
{
    private readonly int _n;
    private readonly RingBuffer _buffer;
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count > _n;

    /// <summary>
    /// Initializes a new Lag node with specified delay.
    /// </summary>
    /// <param name="n">Number of bars to delay by (0 = identity, must be &gt;= 0)</param>
    public Lag(int n = 1)
    {
        if (n < 0)
        {
            throw new ArgumentException("Delay must be >= 0", nameof(n));
        }

        _n = n;
        _buffer = new RingBuffer(n + 1);
        Name = $"Lag({n})";
        WarmupPeriod = n + 1;
    }

    /// <summary>
    /// Initializes a new Lag node with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="n">Number of bars to delay by</param>
    public Lag(ITValuePublisher source, int n = 1) : this(n)
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

        double result = _buffer.Count > _n ? _buffer[0] : 0.0;

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

    public static TSeries Batch(TSeries source, int n = 1)
    {
        var indicator = new Lag(n);
        return indicator.Update(source);
    }

    /// <summary>
    /// Delays a span of values by n samples.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int n = 1)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (n < 0)
        {
            throw new ArgumentException("Delay must be >= 0", nameof(n));
        }

        int len = source.Length;
        double[]? rented = null;
#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
        Span<double> sanitized = len <= 256
            ? stackalloc double[len]
            : (rented = System.Buffers.ArrayPool<double>.Shared.Rent(len)).AsSpan(0, len);
#pragma warning restore S1121

        try
        {
            // First pass: sanitize NaN/Infinity by last-valid substitution.
            double lastValid = 0.0;
            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }

                sanitized[i] = lastValid;
            }

            // Second pass: shift by n, zero-filling the warmup region.
            for (int i = 0; i < len; i++)
            {
                output[i] = i >= n ? sanitized[i - n] : 0.0;
            }
        }
        finally
        {
            if (rented != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    public static (TSeries Results, Lag Indicator) Calculate(TSeries source, int n = 1)
    {
        var indicator = new Lag(n);
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
