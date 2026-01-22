// SIGMOID: Logistic Function
// Activation function that maps any real value to (0, 1)
// Formula: S(x) = 1 / (1 + exp(-k * (x - x0)))

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// SIGMOID: Logistic Function
/// Maps any real-valued input to the range (0, 1) using the logistic function.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output always between 0 and 1 (exclusive)
/// - S-shaped curve centered at x0
/// - Steepness controlled by parameter k
/// - Commonly used for probability-like outputs and neural networks
/// </remarks>
[SkipLocalsInit]
public sealed class Sigmoid : AbstractBase
{
    private readonly double _k;
    private readonly double _x0;

    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => true;  // No warmup needed

    /// <summary>
    /// Initializes a new Sigmoid indicator with specified steepness and midpoint.
    /// </summary>
    /// <param name="k">Steepness factor (default 1.0). Higher values create steeper transitions.</param>
    /// <param name="x0">Midpoint value where output equals 0.5 (default 0.0).</param>
    public Sigmoid(double k = 1.0, double x0 = 0.0)
    {
        if (k <= 0)
            throw new ArgumentException("Steepness (k) must be positive", nameof(k));

        _k = k;
        _x0 = x0;
        Name = $"Sigmoid({k:F2},{x0:F2})";
        WarmupPeriod = 0;
    }

    /// <summary>
    /// Initializes a new Sigmoid indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="k">Steepness factor (default 1.0)</param>
    /// <param name="x0">Midpoint value (default 0.0)</param>
    public Sigmoid(ITValuePublisher source, double k = 1.0, double x0 = 0.0) : this(k, x0)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeSigmoid(double x, double k, double x0)
    {
        double exponent = -k * (x - x0);
        // Guard against overflow: exp(>709) overflows, exp(<-709) underflows to 0
        if (exponent > 700) return 0.0;  // exp(-700) ≈ 0
        if (exponent < -700) return 1.0; // 1/(1+0) = 1
        return 1.0 / (1.0 + Math.Exp(exponent));
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
            result = ComputeSigmoid(value, _k, _x0);
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

    public static TSeries Calculate(TSeries source, double k = 1.0, double x0 = 0.0)
    {
        var indicator = new Sigmoid(k, x0);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates Sigmoid over a span of values.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double k = 1.0, double x0 = 0.0)
    {
        if (source.Length == 0)
            throw new ArgumentException("Source cannot be empty", nameof(source));
        if (output.Length < source.Length)
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        if (k <= 0)
            throw new ArgumentException("Steepness (k) must be positive", nameof(k));

        double lastValid = 0.5;  // Sigmoid(x0) = 0.5
        int i = 0;

        // SIMD path for AVX2 - sigmoid requires exp(), so vectorization is limited
        // Using scalar computation with potential for future SVML support
        if (Avx2.IsSupported && source.Length >= Vector256<double>.Count)
        {
            // For now, process in scalar due to exp() dependency
            // Future: could use Intel SVML or approximate methods
        }

        // Scalar path
        for (; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                double result = ComputeSigmoid(val, k, x0);
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
