using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MAPE: Mean Absolute Percentage Error
/// </summary>
/// <remarks>
/// MAPE measures the average absolute percentage error between actual and predicted values.
/// It expresses accuracy as a percentage, making it scale-independent.
///
/// Formula:
/// MAPE = (100/n) * Σ|((actual - predicted) / actual)|
///
/// Key properties:
/// - Scale-independent (expressed as percentage)
/// - Cannot be calculated when actual = 0
/// - Asymmetric: penalizes under-predictions more than over-predictions
/// - Undefined for zero actual values
/// </remarks>
[SkipLocalsInit]
public sealed class Mape : BiInputIndicatorBase
{
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates MAPE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mape(int period) : base(period, $"Mape({period})") { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        // Avoid division by zero - use small epsilon if actual is zero
        double divisor = Math.Abs(actual) < Epsilon ? Epsilon : actual;
        return 100.0 * Math.Abs((actual - predicted) / divisor);
    }

    /// <summary>
    /// Calculates MAPE for entire series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Batch calculation using percentage error computation with rolling mean.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        ValidateBatchInputs(actual, predicted, output, period);

        int len = actual.Length;
        if (len == 0) return;

        const int StackAllocThreshold = 256;
        Span<double> percentErrors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ErrorHelpers.ComputePercentageErrors(actual, predicted, percentErrors, Epsilon);
        ErrorHelpers.ApplyRollingMean(percentErrors, output, period);
    }
}