// CHANGE: Relative price movement over lookback period
// Calculates percentage change: (current - past) / past

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// CHANGE: Relative Price Change
/// Calculates the percentage change between current value and value N periods ago.
/// Formula: (current - past) / past
/// </summary>
/// <remarks>
/// Key properties:
/// - Returns relative price movement as a decimal (multiply by 100 for percent)
/// - Useful for momentum measurement, rate of change analysis
/// - Can be validated against TA-Lib ROC function (when multiplied by 100)
/// - Returns 0 when past value is 0 to avoid division by zero
/// </remarks>
[SkipLocalsInit]
public sealed class Change : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count > _period;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="period">Lookback period (must be >= 1)</param>
    public Change(int period = 1)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period + 1);
        Name = $"Change({period})";
        WarmupPeriod = period + 1;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Lookback period</param>
    public Change(ITValuePublisher source, int period = 1) : this(period)
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

        double result;
        if (_buffer.Count <= _period)
        {
            result = 0.0;
        }
        else
        {
            double past = _buffer[0];
            result = past != 0.0 ? (value - past) / past : 0.0;
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
            Update(new TValue(time, value: source[i]), isNew: true);
            time += interval;
        }
    }

    public static TSeries Calculate(TSeries source, int period = 1)
    {
        var indicator = new Change(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates relative change over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 1)
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

        // Use ArrayPool for large periods to track past valid values
        const int StackAllocThreshold = 256;
        double[]? pastValidRented = null;

#pragma warning disable S1121
        Span<double> pastValidBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : (pastValidRented = System.Buffers.ArrayPool<double>.Shared.Rent(period)).AsSpan(0, period);
#pragma warning restore S1121

        try
        {
            double lastValidCurrent = 0.0;
            int bufferIdx = 0;
            pastValidBuffer.Fill(0.0);

            for (int i = 0; i < source.Length; i++)
            {
                // Handle non-finite values by substitution for current
                double current = source[i];
                if (!double.IsFinite(current))
                {
                    current = lastValidCurrent;
                }
                else
                {
                    lastValidCurrent = current;
                }

                if (i < period)
                {
                    output[i] = 0.0;
                    // Store valid values for later past lookups
                    pastValidBuffer[i] = current;
                }
                else
                {
                    // Get past value with proper tracking
                    double past = source[i - period];
                    if (!double.IsFinite(past))
                    {
                        // Use the tracked valid value from period bars ago
                        past = pastValidBuffer[bufferIdx];
                    }

                    output[i] = past != 0.0 ? (current - past) / past : 0.0;

                    // Update circular buffer with current valid value for future past lookups
                    pastValidBuffer[bufferIdx] = current;
                    bufferIdx = (bufferIdx + 1) % period;
                }
            }
        }
        finally
        {
            if (pastValidRented != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(pastValidRented);
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
