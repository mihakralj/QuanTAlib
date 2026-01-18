using System.Buffers;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MAE: Mean Absolute Error
/// </summary>
/// <remarks>
/// MAE measures the average magnitude of errors between paired observations,
/// without considering their direction. It is the mean of the absolute differences
/// between actual and predicted values.
///
/// Formula:
/// MAE = (1/n) * Σ|actual - predicted|
///
/// Uses a RingBuffer for O(1) streaming updates with running sum.
///
/// Key properties:
/// - Always non-negative (MAE ≥ 0)
/// - Same units as the original data
/// - Less sensitive to outliers than MSE/RMSE
/// - MAE = 0 indicates perfect prediction
/// </remarks>
[SkipLocalsInit]
public sealed class Mae : BiInputIndicatorBase
{
    /// <summary>
    /// Creates MAE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mae(int period) : base(period, $"Mae({period})") { }

    /// <summary>
    /// Computes absolute error: |actual - predicted|
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
        => Math.Abs(actual - predicted);

    /// <summary>
    /// Calculates MAE for the entire series pair.
    /// </summary>
    /// <param name="actual">Actual values series</param>
    /// <param name="predicted">Predicted values series</param>
    /// <param name="period">MAE period</param>
    /// <returns>MAE series</returns>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Calculates MAE in-place using pre-allocated spans.
    /// Uses SIMD acceleration when available.
    /// </summary>
    /// <param name="actual">Actual values</param>
    /// <param name="predicted">Predicted values</param>
    /// <param name="output">Output span (must be same length as inputs)</param>
    /// <param name="period">MAE period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        ValidateBatchInputs(actual, predicted, output, period);
        if (actual.Length == 0) return;

        // Allocate temporary buffer for absolute errors
        const int StackAllocThreshold = 256;
        int len = actual.Length;
        if (len <= StackAllocThreshold)
        {
            Span<double> absErrors = stackalloc double[len];
            ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, absErrors);
            ErrorHelpers.ApplyRollingMean(absErrors, output, period);
        }
        else
        {
            double[] rented = ArrayPool<double>.Shared.Rent(len);
            try
            {
                Span<double> absErrors = rented.AsSpan(0, len);
                ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, absErrors);
                ErrorHelpers.ApplyRollingMean(absErrors, output, period);
            }
            finally
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }
}