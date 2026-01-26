using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PseudoHuber: Pseudo-Huber Loss (Charbonnier Loss)
/// </summary>
/// <remarks>
/// The Pseudo-Huber loss is a smooth approximation to the Huber loss function.
/// Unlike Huber loss which has a piecewise definition, Pseudo-Huber is smooth
/// and differentiable everywhere, making it ideal for gradient-based optimization.
///
/// Formula:
/// PseudoHuber = δ² * (√(1 + (error/δ)²) - 1)
///
/// Key properties:
/// - Smooth and continuously differentiable everywhere
/// - Approximates L2 (squared error) for small errors
/// - Approximates L1 (absolute error) for large errors
/// - δ (delta) controls the transition point
/// - More computationally efficient than Huber's conditional logic
/// - Also known as Charbonnier loss in image processing
/// </remarks>
[SkipLocalsInit]
public sealed class PseudoHuber : BiInputIndicatorBase
{
    private readonly double _deltaSquared;

    /// <summary>
    /// Gets the delta parameter (transition scale).
    /// </summary>
    public double Delta { get; }

    /// <summary>
    /// Creates a Pseudo-Huber Loss indicator.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    /// <param name="delta">Scale parameter controlling transition smoothness (must be > 0). Default 1.0</param>
    public PseudoHuber(int period, double delta = 1.0)
        : base(period, $"PseudoHuber({period},{delta:F3})")
    {
        if (delta <= 0)
        {
            throw new ArgumentException("Delta must be positive", nameof(delta));
        }

        Delta = delta;
        _deltaSquared = delta * delta;
    }

    /// <summary>
    /// Computes Pseudo-Huber loss: δ² * (√(1 + (error/δ)²) - 1)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double diff = actual - predicted;
        double ratio = diff / Delta;
        double sqrtTerm = Math.Sqrt(1.0 + ratio * ratio);
        return Math.FusedMultiplyAdd(_deltaSquared, sqrtTerm, -_deltaSquared);
    }

    /// <summary>
    /// Calculates Pseudo-Huber Loss for two time series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period, double delta = 1.0)
    {
        if (actual.Count != predicted.Count)
        {
            throw new ArgumentException("Actual and predicted series must have the same length", nameof(predicted));
        }

        int len = actual.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(actual.Values, predicted.Values, vSpan, period, delta);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation of Pseudo-Huber Loss using shared error helpers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period, double delta = 1.0)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (delta <= 0)
        {
            throw new ArgumentException("Delta must be positive", nameof(delta));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        // Pre-compute Pseudo-Huber errors using shared helper
        const int StackAllocThreshold = 256;
        Span<double> errors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ErrorHelpers.ComputePseudoHuberErrors(actual, predicted, errors, delta);

        // Apply rolling mean
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }
}
