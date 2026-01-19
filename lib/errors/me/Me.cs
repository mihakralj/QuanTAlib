using System.Buffers;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ME: Mean Error (also known as Mean Bias Error)
/// </summary>
/// <remarks>
/// ME measures the average error between actual and predicted values,
/// preserving the sign to indicate systematic bias in predictions.
///
/// Formula:
/// ME = (1/n) * Σ(actual - predicted)
///
/// Key properties:
/// - Can be positive or negative
/// - Positive ME indicates under-prediction (actual > predicted)
/// - Negative ME indicates over-prediction (actual &lt; predicted)
/// - ME = 0 indicates no systematic bias (but not necessarily accurate predictions)
/// - Errors can cancel out, hiding large individual errors
/// </remarks>
[SkipLocalsInit]
public sealed class Me : BiInputIndicatorBase
{
    /// <summary>
    /// Creates ME with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Me(int period) : base(period, $"Me({period})") { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        // ME preserves sign: actual - predicted
        return actual - predicted;
    }

    /// <summary>
    /// Calculates ME for entire series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Batch calculation using signed error computation with rolling mean.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        ValidateBatchInputs(actual, predicted, output, period);

        int len = actual.Length;
        if (len == 0) return;

        const int StackAllocThreshold = 256;
        if (len <= StackAllocThreshold)
        {
            Span<double> errors = stackalloc double[len];
            ErrorHelpers.ComputeSignedErrors(actual, predicted, errors);
            ErrorHelpers.ApplyRollingMean(errors, output, period);
        }
        else
        {
            double[] rented = ArrayPool<double>.Shared.Rent(len);
            try
            {
                Span<double> errors = rented.AsSpan(0, len);
                ErrorHelpers.ComputeSignedErrors(actual, predicted, errors);
                ErrorHelpers.ApplyRollingMean(errors, output, period);
            }
            finally
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }
}