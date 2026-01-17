// NORMALIZE: Min-Max Normalization
// Scales values to [0, 1] range using min-max scaling over a lookback period
// Formula: (x - min) / (max - min)

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NORMALIZE: Min-Max Normalization
/// Scales values to the range [0, 1] using min-max normalization over a lookback period.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always between 0 and 1 (inclusive when value equals min or max)
/// - Uses rolling window to track min and max
/// - Division by zero (flat range) returns 0.5 as neutral value
/// - Commonly used for feature scaling and bounded indicators
/// </remarks>
[SkipLocalsInit]
public sealed class Normalize : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    private record struct State(double LastValidNorm, double Min, double Max);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _period;

    /// <param name="period">Lookback period for min/max calculation (default 14)</param>
    public Normalize(int period = 14)
    {
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Normalize({period})";
        WarmupPeriod = period;
        _state = new State(0.5, double.MaxValue, double.MinValue);
        _p_state = _state;
    }

    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback period (default 14)</param>
    public Normalize(ITValuePublisher source, int period = 14) : this(period)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double min, double max) FindMinMax(ReadOnlySpan<double> values)
    {
        if (values.Length == 0)
            return (double.MaxValue, double.MinValue);

        double min = values[0];
        double max = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            double v = values[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        return (min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
            _p_state = _state;
        else
            _state = _p_state;

        double value = input.Value;
        double result;

        if (double.IsFinite(value))
        {
            _buffer.Add(value, isNew);

            // Find min and max in the buffer
            var (min, max) = FindMinMax(_buffer.GetSpan());
            double range = max - min;

            if (range > 0)
            {
                result = (value - min) / range;
            }
            else
            {
                // Flat range: return 0.5 as neutral
                result = 0.5;
            }

            _state = new State(result, min, max);
        }
        else
        {
            result = _state.LastValidNorm;
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

    public static TSeries Calculate(TSeries source, int period = 14)
    {
        var indicator = new Normalize(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Min-Max Normalization over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 14)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        double lastValid = 0.5;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];

            if (!double.IsFinite(val))
            {
                output[i] = lastValid;
                continue;
            }

            // Determine window bounds
            int start = Math.Max(0, i - period + 1);
            int windowLength = i - start + 1;

            // Find min/max in window
            double min = source[start];
            double max = source[start];

            for (int j = start + 1; j <= i; j++)
            {
                double v = source[j];
                if (double.IsFinite(v))
                {
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            double range = max - min;
            double result = range > 0 ? (val - min) / range : 0.5;

            lastValid = result;
            output[i] = result;
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(0.5, double.MaxValue, double.MinValue);
        _p_state = _state;
        Last = default;
    }
}
