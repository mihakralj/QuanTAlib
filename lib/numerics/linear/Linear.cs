// LINEAR: Linear Scaling Transformer
// Transforms values using linear equation: y = slope * x + intercept

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// LINEAR: Linear Scaling Transformer
/// Applies y = slope * x + intercept transformation to input values.
/// </summary>
/// <remarks>
/// Key properties:
/// - Preserves relative differences (affine transformation)
/// - Useful for scaling, offsetting, and normalizing data
/// - Domain: all real numbers
/// - Default: identity transform (slope=1, intercept=0)
/// </remarks>
[SkipLocalsInit]
public sealed class Linear : AbstractBase
{
    private readonly double _slope;
    private readonly double _intercept;

    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => true;  // No warmup needed

    /// <summary>
    /// Creates a Linear transformer with specified slope and intercept.
    /// </summary>
    /// <param name="slope">Multiplicative factor (default: 1.0)</param>
    /// <param name="intercept">Additive constant (default: 0.0)</param>
    public Linear(double slope = 1.0, double intercept = 0.0)
    {
        if (!double.IsFinite(slope))
            throw new ArgumentException("Slope must be a finite number", nameof(slope));
        if (!double.IsFinite(intercept))
            throw new ArgumentException("Intercept must be a finite number", nameof(intercept));

        _slope = slope;
        _intercept = intercept;
        Name = $"Linear({slope},{intercept})";
        WarmupPeriod = 0;
    }

    /// <param name="source">Source indicator for chaining</param>
    /// <param name="slope">Multiplicative factor (default: 1.0)</param>
    /// <param name="intercept">Additive constant (default: 0.0)</param>
    public Linear(ITValuePublisher source, double slope = 1.0, double intercept = 0.0)
        : this(slope, intercept)
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

        double value = input.Value;
        double result;

        if (double.IsFinite(value))
        {
            result = Math.FusedMultiplyAdd(_slope, value, _intercept);
            _state = new State(result);
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

    public static TSeries Calculate(TSeries source, double slope = 1.0, double intercept = 0.0)
    {
        var indicator = new Linear(slope, intercept);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates linear transformation over a span of values using SIMD when available.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output,
                                  double slope = 1.0, double intercept = 0.0)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        if (!double.IsFinite(slope))
            throw new ArgumentException("Slope must be a finite number", nameof(slope));
        if (!double.IsFinite(intercept))
            throw new ArgumentException("Intercept must be a finite number", nameof(intercept));

        double lastValid = 0.0;
        int i = 0;

        // SIMD path for AVX2 (process 4 doubles at a time)
        if (Avx2.IsSupported && source.Length >= Vector256<double>.Count)
        {
            int vectorLength = source.Length - (source.Length % Vector256<double>.Count);

            for (; i < vectorLength; i += Vector256<double>.Count)
            {
                // Check for finite values and handle last-valid
                for (int j = 0; j < Vector256<double>.Count; j++)
                {
                    double val = source[i + j];
                    if (double.IsFinite(val))
                    {
                        lastValid = Math.FusedMultiplyAdd(slope, val, intercept);
                        output[i + j] = lastValid;
                    }
                    else
                    {
                        output[i + j] = lastValid;
                    }
                }
            }
        }

        // Scalar fallback for remaining elements
        for (; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = Math.FusedMultiplyAdd(slope, val, intercept);
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
