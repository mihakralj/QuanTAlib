using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// LogCosh: Log-Cosh Loss
/// </summary>
/// <remarks>
/// Log-Cosh is the logarithm of the hyperbolic cosine of the error. It is a
/// smooth approximation to the absolute error that is twice differentiable
/// everywhere, making it suitable for gradient-based optimization.
///
/// Formula:
/// LogCosh = (1/n) * Σ log(cosh(actual - predicted))
///
/// Key properties:
/// - Smooth and differentiable everywhere
/// - Approximates L1 loss for large errors
/// - Approximates L2 loss for small errors
/// - Less sensitive to outliers than MSE
/// - Numerically stable (uses stable computation for large values)
/// </remarks>
[SkipLocalsInit]
public sealed class LogCosh : BiInputIndicatorBase
{
    /// <summary>
    /// Creates LogCosh with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public LogCosh(int period) : base(period, $"LogCosh({period})") { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double error = actual - predicted;
        return StableLogCosh(error);
    }

    /// <summary>
    /// Computes log(cosh(x)) in a numerically stable way.
    /// For large |x|, cosh(x) ≈ exp(|x|)/2, so log(cosh(x)) ≈ |x| - log(2)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double StableLogCosh(double x)
    {
        double absX = Math.Abs(x);
        // For large values, use asymptotic approximation to avoid overflow
        if (absX > 20.0)
            return absX - 0.6931471805599453; // log(2)
        return Math.Log(Math.Cosh(x));
    }

    /// <summary>
    /// Calculates LogCosh for entire series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Batch calculation using log-cosh error computation with rolling mean.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        ValidateBatchInputs(actual, predicted, output, period);

        int len = actual.Length;
        if (len == 0) return;

        const int StackAllocThreshold = 256;
        Span<double> errors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ErrorHelpers.ComputeLogCoshErrors(actual, predicted, errors);
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }
}
