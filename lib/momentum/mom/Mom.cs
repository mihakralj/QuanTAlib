using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Momentum (MOM), which measures the absolute price change over a specified lookback period.
/// </summary>
/// <remarks>
/// MOM Formula:
/// <c>MOM = Price - Price[N]</c>.
///
/// Positive values indicate upward momentum; negative values indicate downward momentum.
/// This implementation is optimized for streaming updates with O(1) per bar.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="mom.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Mom : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private record struct State(double LastValid);
    private State _state, _p_state;
    private ITValuePublisher? _source;
    private bool _disposed;

    /// <summary>
    /// True when the buffer has enough data to compute valid momentum values.
    /// </summary>
    public override bool IsHot => _buffer.Count > _period;

    /// <summary>
    /// Initializes a new Momentum indicator with specified lookback period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 1)</param>
    public Mom(int period = 10)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period + 1);
        Name = $"Mom({period})";
        WarmupPeriod = period + 1;
    }

    /// <summary>
    /// Initializes a new Momentum indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback period</param>
    public Mom(ITValuePublisher source, int period = 10) : this(period)
    {
        _source = source;
        _source.Pub += HandleUpdate;
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

        double result;
        if (_buffer.Count <= _period)
        {
            result = 0.0;
        }
        else
        {
            double past = _buffer[0];
            result = value - past;
        }

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

    public static TSeries Batch(TSeries source, int period = 10)
    {
        var indicator = new Mom(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates momentum (absolute change) over a span of values.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 10)
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

        for (int i = 0; i < source.Length; i++)
        {
            output[i] = i < period ? 0.0 : source[i] - source[i - period];
        }
    }

    public static (TSeries Results, Mom Indicator) Calculate(TSeries source, int period = 10)
    {
        var indicator = new Mom(period);
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

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= HandleUpdate;
                _source = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
