using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// SMAPE: Symmetric Mean Absolute Percentage Error
/// </summary>
/// <remarks>
/// SMAPE is a percentage-based error metric that treats over-predictions and
/// under-predictions symmetrically. Unlike MAPE, it uses the average of actual
/// and predicted values in the denominator.
///
/// Formula:
/// SMAPE = (200/n) * Σ(|actual - predicted| / (|actual| + |predicted|))
///
/// Key properties:
/// - Bounded between 0% and 200%
/// - Symmetric: same penalty for over/under-prediction
/// - Handles zero values better than MAPE (when only one is zero)
/// - Scale-independent (expressed as percentage)
/// </remarks>
[SkipLocalsInit]
public sealed class Smape : BiInputIndicatorBase
{
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates SMAPE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Smape(int period) : base(period, $"Smape({period})") { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        // SMAPE: 200 * |actual - predicted| / (|actual| + |predicted|)
        double absDiff = Math.Abs(actual - predicted);
        double sumAbs = Math.Abs(actual) + Math.Abs(predicted);
        return sumAbs > Epsilon ? 200.0 * absDiff / sumAbs : 0.0;
    }

    /// <summary>
    /// Calculates SMAPE for entire series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Batch calculation using symmetric percentage error computation with rolling mean.
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
        Span<double> symErrors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        // Compute symmetric percentage errors with 200.0 multiplier (not 100.0 from helper)
        ComputeSmapeErrors(actual, predicted, symErrors);
        ErrorHelpers.ApplyRollingMean(symErrors, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSmapeErrors(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output)
    {
        int len = actual.Length;
        double lastValidActual = 0, lastValidPredicted = 0;

        for (int i = 0; i < len; i++)
        {
            if (double.IsFinite(actual[i]))
            {
                lastValidActual = actual[i];
                break;
            }
        }

        for (int i = 0; i < len; i++)
        {
            if (double.IsFinite(predicted[i]))
            {
                lastValidPredicted = predicted[i];
                break;
            }
        }

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                lastValidActual = act;
            }
            else
            {
                act = lastValidActual;
            }

            if (double.IsFinite(pred))
            {
                lastValidPredicted = pred;
            }
            else
            {
                pred = lastValidPredicted;
            }

            double absDiff = Math.Abs(act - pred);
            double sumAbs = Math.Abs(act) + Math.Abs(pred);
            output[i] = sumAbs > Epsilon ? 200.0 * absDiff / sumAbs : 0.0;
        }
    }
}
