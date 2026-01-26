// LINEARTRANS: Linear Scaling Transformer
// Transforms values using linear equation: y = slope * x + intercept

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace QuanTAlib;

/// <summary>
/// LINEARTRANS: Linear Scaling Transformer
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
public sealed class Lineartrans : AbstractBase
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
    public Lineartrans(double slope = 1.0, double intercept = 0.0)
    {
        if (!double.IsFinite(slope))
        {
            throw new ArgumentException("Slope must be a finite number", nameof(slope));
        }

        if (!double.IsFinite(intercept))
        {
            throw new ArgumentException("Intercept must be a finite number", nameof(intercept));
        }

        _slope = slope;
        _intercept = intercept;
        Name = $"Lineartrans({slope},{intercept})";
        WarmupPeriod = 0;
    }

    /// <summary>
    /// Creates a Linear transformer with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="slope">Multiplicative factor (default: 1.0)</param>
    /// <param name="intercept">Additive constant (default: 0.0)</param>
    public Lineartrans(ITValuePublisher source, double slope = 1.0, double intercept = 0.0)
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
        var indicator = new Lineartrans(slope, intercept);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates linear transformation over a span of values using SIMD when available.
    /// Uses FMA intrinsics for y = slope * x + intercept.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output,
                                  double slope = 1.0, double intercept = 0.0)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (!double.IsFinite(slope))
        {
            throw new ArgumentException("Slope must be a finite number", nameof(slope));
        }

        if (!double.IsFinite(intercept))
        {
            throw new ArgumentException("Intercept must be a finite number", nameof(intercept));
        }

        // Check for non-finite values - if any exist, use scalar path only
        // Note: For very large arrays, SIMD-based NaN detection could be faster,
        // but for typical use cases the scalar pre-scan is sufficient
        bool hasNonFinite = false;
        for (int k = 0; k < source.Length && !hasNonFinite; k++)
        {
            hasNonFinite = !double.IsFinite(source[k]);
        }

        double lastValid = 0.0;
        int i = 0;

        // AVX512 FMA path (8 doubles at once)
        // Avx512F.FusedMultiplyAdd is independent of Fma.IsSupported
        if (!hasNonFinite && Avx512F.IsSupported && source.Length >= 8)
        {
            var slopeVec = Vector512.Create(slope);
            var interceptVec = Vector512.Create(intercept);
            int simdEnd = source.Length - (source.Length % 8);

            for (; i < simdEnd; i += 8)
            {
                var vals = Vector512.Create(source.Slice(i, 8));
                var result = Avx512F.FusedMultiplyAdd(slopeVec, vals, interceptVec);
                result.CopyTo(output.Slice(i, 8));
            }
            lastValid = output[simdEnd - 1];
        }
        // AVX2 FMA path (4 doubles at once)
        else if (!hasNonFinite && Fma.IsSupported && source.Length >= 4)
        {
            var slopeVec = Vector256.Create(slope);
            var interceptVec = Vector256.Create(intercept);
            int simdEnd = source.Length - (source.Length % 4);

            for (; i < simdEnd; i += 4)
            {
                var vals = Vector256.Create(source.Slice(i, 4));
                var result = Fma.MultiplyAdd(slopeVec, vals, interceptVec);
                result.CopyTo(output.Slice(i, 4));
            }
            lastValid = output[simdEnd - 1];
        }
        // SSE2 path (2 doubles at once) - fallback for x86/x64 without FMA
        // Note: This path uses Sse2.Multiply followed by Sse2.Add, which incurs two rounding
        // steps unlike the FMA paths above. Results may differ by ~1 ULP compared to FMA
        // on SSE2-only hardware (e.g., older x86/x64 CPUs without AVX2/FMA support).
        else if (!hasNonFinite && Sse2.IsSupported && source.Length >= 2)
        {
            var slopeVec = Vector128.Create(slope);
            var interceptVec = Vector128.Create(intercept);
            int simdEnd = source.Length - (source.Length % 2);

            for (; i < simdEnd; i += 2)
            {
                var vals = Vector128.Create(source.Slice(i, 2));
                var result = Sse2.Add(Sse2.Multiply(slopeVec, vals), interceptVec);
                result.CopyTo(output.Slice(i, 2));
            }
            lastValid = output[simdEnd - 1];
        }
        // ARM64 NEON FMA path (2 doubles at once)
        else if (!hasNonFinite && AdvSimd.Arm64.IsSupported && source.Length >= 2)
        {
            var slopeVec = Vector128.Create(slope);
            var interceptVec = Vector128.Create(intercept);
            int simdEnd = source.Length - (source.Length % 2);

            for (; i < simdEnd; i += 2)
            {
                var vals = Vector128.Create(source.Slice(i, 2));
                var result = AdvSimd.Arm64.FusedMultiplyAdd(interceptVec, vals, slopeVec);
                result.CopyTo(output.Slice(i, 2));
            }
            lastValid = output[simdEnd - 1];
        }

        // Scalar fallback for remaining elements or when non-finite values exist
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
