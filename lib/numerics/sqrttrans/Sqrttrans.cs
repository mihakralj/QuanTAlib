// SQRTTRANS: Square Root Transformer
// Transforms values using the square root function √x

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// SQRTTRANS: Square Root Transformer
/// Applies √x transformation to input values.
/// </summary>
/// <remarks>
/// Key properties:
/// - Inverse of squaring: sqrt(x²) = |x| for x ≥ 0
/// - Compresses large values while expanding small ones
/// - Only defined for non-negative inputs
/// - Useful for variance to standard deviation conversion
/// </remarks>
[SkipLocalsInit]
public sealed class Sqrttrans : AbstractBase
{
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => true;  // No warmup needed

    public Sqrttrans()
    {
        Name = "Sqrttrans";
        WarmupPeriod = 0;
    }

    /// <summary>
    /// Creates a Square Root transformer with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    public Sqrttrans(ITValuePublisher source) : this()
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

        double value = input.Value;
        double result;

        if (double.IsFinite(value) && value >= 0)
        {
            result = Math.Sqrt(value);
            _state = new State(result);
        }
        else
        {
            // For negative values or non-finite, use last valid
            result = _state.LastValid;
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

    public static TSeries Calculate(TSeries source)
    {
        var indicator = new Sqrttrans();
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates square root over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        double lastValid = 0.0;  // sqrt(0) = 0

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val) && val >= 0)
            {
                lastValid = Math.Sqrt(val);
                output[i] = lastValid;
            }
            else
            {
                output[i] = lastValid;
            }
        }
    }

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }
}
