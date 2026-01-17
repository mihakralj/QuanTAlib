// ROC: Rate of Change (Absolute)
// Calculates absolute price change: current - past

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ROC: Rate of Change (Absolute)
/// Calculates the absolute difference between current value and value N periods ago.
/// Formula: current - past
/// </summary>
/// <remarks>
/// Key properties:
/// - Returns absolute price movement in price units
/// - Useful for momentum measurement, trend direction
/// - Different from ROCP (percentage) and ROCR (ratio)
/// - Can be validated against TA-Lib MOM function
/// </remarks>
[SkipLocalsInit]
public sealed class Roc : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count > _period;

    /// <param name="period">Lookback period (must be >= 1)</param>
    public Roc(int period = 9)
    {
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        _period = period;
        _buffer = new RingBuffer(period + 1);
        Name = $"Roc({period})";
        WarmupPeriod = period + 1;
    }

    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback period</param>
    public Roc(ITValuePublisher source, int period = 9) : this(period)
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

    public static TSeries Calculate(TSeries source, int period = 9)
    {
        var indicator = new Roc(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates absolute change over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 9)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        for (int i = 0; i < source.Length; i++)
        {
            if (i < period)
            {
                output[i] = 0.0;
            }
            else
            {
                output[i] = source[i] - source[i - period];
            }
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
