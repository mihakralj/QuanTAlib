// LOGTRANS: Natural Logarithm Transformer
// Transforms values using natural logarithm (base e)

using System.Runtime.CompilerServices;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// LOGTRANS: Natural Logarithm Transformer
/// Applies ln(x) transformation to input values.
/// </summary>
/// <remarks>
/// Key properties:
/// - Compresses large values, expands small values
/// - Useful for transforming multiplicative relationships to additive
/// - Domain: x > 0 (non-positive inputs use last valid value)
/// - Common in financial returns: ln(P_t / P_{t-1})
/// </remarks>
[SkipLocalsInit]
public sealed class Logtrans : AbstractBase
{
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => true;  // No warmup needed

    public Logtrans()
    {
        Name = "Logtrans";
        WarmupPeriod = 0;
    }

    /// <param name="source">Source indicator for chaining</param>
    public Logtrans(ITValuePublisher source) : this()
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

        // Handle non-positive and non-finite values
        double value = input.Value;
        double result;

        if (double.IsFinite(value) && value > 0)
        {
            result = Math.Log(value);
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

    public static TSeries Calculate(TSeries source)
    {
        var indicator = new Logtrans();
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates natural logarithm over a span of values using SIMD when available.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));

        double lastValid = 0.0;
        int i = 0;

        // SIMD path for AVX2 (process 4 doubles at a time)
        if (Avx2.IsSupported && source.Length >= Vector256<double>.Count)
        {
            int vectorLength = source.Length - (source.Length % Vector256<double>.Count);

            for (; i < vectorLength; i += Vector256<double>.Count)
            {
                // Process scalar for proper last-valid handling (Logtrans has no SIMD intrinsic)
                for (int j = 0; j < Vector256<double>.Count; j++)
                {
                    double val = source[i + j];
                    if (double.IsFinite(val) && val > 0)
                    {
                        lastValid = Math.Log(val);
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
            if (double.IsFinite(val) && val > 0)
            {
                lastValid = Math.Log(val);
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