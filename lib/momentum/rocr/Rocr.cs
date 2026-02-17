using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ROCR: Rate of Change Ratio
/// </summary>
/// <remarks>
/// Price ratio momentum: ratio between current and N-period-ago value.
/// Returns 1.0 for no change, greater than 1 for increase, less than 1 for decrease.
/// See ROC for absolute change, ROCP for percentage.
///
/// Calculation: <c>ROCR = Price / Price[N]</c>.
/// </remarks>
/// <seealso href="Rocr.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Rocr : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private record struct State(double LastValid);
    private State _state, _p_state;
    private ITValuePublisher? _source;
    private bool _disposed;

    public override bool IsHot => _buffer.Count > _period;

    /// <summary>
    /// Initializes a new Rate of Change Ratio indicator with specified lookback period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 1)</param>
    public Rocr(int period = 9)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period + 1);
        Name = $"Rocr({period})";
        WarmupPeriod = period + 1;
    }

    /// <summary>
    /// Initializes a new Rate of Change Ratio indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback period</param>
    public Rocr(ITValuePublisher source, int period = 9) : this(period)
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
            result = 1.0; // Default ratio during warmup
        }
        else
        {
            double past = _buffer[0];
            result = past != 0 ? value / past : 1.0; // skipcq: CS-R1077 - Exact-zero IEEE 754 div guard
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

    public static TSeries Batch(TSeries source, int period = 9)
    {
        var indicator = new Rocr(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates rate of change ratio over a span of values.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 9)
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
            if (i < period)
            {
                output[i] = 1.0; // Default ratio during warmup
            }
            else
            {
                double past = source[i - period];
                output[i] = past != 0 ? source[i] / past : 1.0; // skipcq: CS-R1077 - Exact-zero IEEE 754 div guard
            }
        }
    }

    public static (TSeries Results, Rocr Indicator) Calculate(TSeries source, int period = 9)
    {
        var indicator = new Rocr(period);
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