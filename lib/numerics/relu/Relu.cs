// RELU: Rectified Linear Unit
// Activation function that returns max(0, x)

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// RELU: Rectified Linear Unit
/// Applies max(0, x) transformation to input values.
/// </summary>
/// <remarks>
/// Key properties:
/// - Zero for negative inputs, passthrough for positive
/// - Commonly used as activation function in neural networks
/// - Computationally efficient: simple comparison
/// - Non-linear, allowing networks to learn complex patterns
/// </remarks>
[SkipLocalsInit]
public sealed class Relu : AbstractBase
{
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => true;  // No warmup needed

    public Relu()
    {
        Name = "ReLU";
        WarmupPeriod = 0;
    }

    /// <param name="source">Source indicator for chaining</param>
    public Relu(ITValuePublisher source) : this()
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
            result = Math.Max(0.0, value);
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
        var indicator = new Relu();
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates ReLU over a span of values with SIMD optimization.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));

        double lastValid = 0.0;
        int i = 0;

        // SIMD path for AVX2
        if (Avx2.IsSupported && source.Length >= Vector256<double>.Count)
        {
            Vector256<double> zero = Vector256<double>.Zero;
            int simdLength = source.Length - (source.Length % Vector256<double>.Count);

            for (; i < simdLength; i += Vector256<double>.Count)
            {
                Vector256<double> vec = Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(source.Slice(i)));
                Vector256<double> result = Avx.Max(zero, vec);
                result.StoreUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(output.Slice(i)));
            }
            // Track lastValid from SIMD output
            if (simdLength > 0)
            {
                lastValid = output[simdLength - 1];
            }
        }

        // Scalar fallback for remaining elements
        for (; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                double result = Math.Max(0.0, val);
                lastValid = result;
                output[i] = result;
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
