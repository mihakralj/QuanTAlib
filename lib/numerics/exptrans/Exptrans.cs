// EXPTRANS: Exponential Transformer
// Transforms values using the exponential function e^x

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// EXPTRANS: Exponential Transformer
/// Applies e^x transformation to input values.
/// </summary>
/// <remarks>
/// Key properties:
/// - Inverse of natural logarithm: exp(ln(x)) = x
/// - Maps additive relationships to multiplicative
/// - Always positive output for any finite input
/// - Useful for converting log returns to price ratios
/// </remarks>
[SkipLocalsInit]
public sealed class Exptrans : AbstractBase
{
    private record struct State(double LastValid = 1.0);  // exp(0) = 1
    private State _state = new(1.0), _p_state = new(1.0);

    public override bool IsHot => true;  // No warmup needed

    public Exptrans()
    {
        Name = "Exptrans";
        WarmupPeriod = 0;
    }

    /// <summary>
    /// Creates Exptrans with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    public Exptrans(ITValuePublisher source) : this()
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

        if (double.IsFinite(value))
        {
            result = Math.Exp(value);
            // Check for overflow (exp can produce infinity for large inputs)
            if (double.IsFinite(result))
            {
                _state = new State(result);
            }
            else
            {
                result = _state.LastValid;
            }
        }
        else
        {
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
        var indicator = new Exptrans();
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates exponential over a span of values.
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

        double lastValid = 1.0;  // exp(0) = 1

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                double result = Math.Exp(val);
                if (double.IsFinite(result))
                {
                    lastValid = result;
                    output[i] = result;
                }
                else
                {
                    output[i] = lastValid;
                }
            }
            else
            {
                output[i] = lastValid;
            }
        }
    }

    public override void Reset()
    {
        _state = new(1.0);
        _p_state = new(1.0);
        Last = default;
    }
}
