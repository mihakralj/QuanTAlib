using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// RMSE: Root Mean Squared Error
/// </summary>
/// <remarks>
/// RMSE is the square root of MSE, bringing the error metric back to the
/// original units of the data while retaining the outlier sensitivity
/// of squared errors.
///
/// Formula:
/// RMSE = √((1/n) * Σ(actual - predicted)²) = √MSE
///
/// Uses a RingBuffer for O(1) streaming updates with running sum.
///
/// Key properties:
/// - Always non-negative (RMSE ≥ 0)
/// - Same units as the original data
/// - Heavily penalizes outliers due to squaring before averaging
/// - RMSE = 0 indicates perfect prediction
/// </remarks>
[SkipLocalsInit]
public sealed class Rmse : BiInputIndicatorBase
{
    /// <summary>
    /// Creates RMSE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Rmse(int period) : base(period, $"Rmse({period})") { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double diff = actual - predicted;
        return diff * diff;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double PostProcess(double mean) => Math.Sqrt(mean);

    /// <summary>
    /// Calculates RMSE for entire series.
    /// </summary>
    public static TSeries Batch(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Batch calculation using SIMD-accelerated squared error computation with sqrt of rolling mean.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        ValidateBatchInputs(actual, predicted, output, period);

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        const int StackAllocThreshold = 256;
        Span<double> sqErrors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ErrorHelpers.ComputeSquaredErrors(actual, predicted, sqErrors);
        ErrorHelpers.ApplyRollingMeanSqrt(sqErrors, output, period);
    }

    public static (TSeries Results, Rmse Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Rmse(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}