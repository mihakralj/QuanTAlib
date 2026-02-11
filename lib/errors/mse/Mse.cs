using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MSE: Mean Squared Error
/// </summary>
/// <remarks>
/// MSE measures the average of the squares of the errors between actual and
/// predicted values. It penalizes larger errors more heavily than MAE.
///
/// Formula:
/// MSE = (1/n) * Σ(actual - predicted)²
///
/// Uses a RingBuffer for O(1) streaming updates with running sum.
///
/// Key properties:
/// - Always non-negative (MSE ≥ 0)
/// - Units are squared (e.g., if data is in dollars, MSE is in dollars²)
/// - Heavily penalizes outliers due to squaring
/// - MSE = 0 indicates perfect prediction
/// </remarks>
[SkipLocalsInit]
public sealed class Mse : BiInputIndicatorBase
{
    /// <summary>
    /// Creates MSE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mse(int period) : base(period, $"Mse({period})") { }

    /// <summary>
    /// Computes squared error: (actual - predicted)²
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double diff = actual - predicted;
        return diff * diff;
    }

    /// <summary>
    /// Calculates MSE for the entire series pair.
    /// </summary>
    /// <param name="actual">Actual values series</param>
    /// <param name="predicted">Predicted values series</param>
    /// <param name="period">MSE period</param>
    /// <returns>MSE series</returns>
    public static TSeries Batch(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Calculates MSE in-place using pre-allocated spans.
    /// Uses SIMD acceleration when available.
    /// </summary>
    /// <param name="actual">Actual values</param>
    /// <param name="predicted">Predicted values</param>
    /// <param name="output">Output span (must be same length as inputs)</param>
    /// <param name="period">MSE period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        ValidateBatchInputs(actual, predicted, output, period);
        if (actual.Length == 0)
        {
            return;
        }

        // Allocate temporary buffer for squared errors
        const int StackAllocThreshold = 256;
        Span<double> sqErrors = actual.Length <= StackAllocThreshold
            ? stackalloc double[actual.Length]
            : new double[actual.Length];

        // Compute squared errors using shared SIMD helper
        ErrorHelpers.ComputeSquaredErrors(actual, predicted, sqErrors);

        // Apply rolling mean using shared helper
        ErrorHelpers.ApplyRollingMean(sqErrors, output, period);
    }

    public static (TSeries Results, Mse Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Mse(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}