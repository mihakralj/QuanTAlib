using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MSLE: Mean Squared Logarithmic Error
/// </summary>
/// <remarks>
/// MSLE measures the ratio between actual and predicted values using logarithms,
/// penalizing under-predictions more than over-predictions of the same magnitude.
/// Useful when targets span several orders of magnitude.
///
/// Formula:
/// MSLE = (1/n) * Σ(log(1 + actual) - log(1 + predicted))²
///
/// Key properties:
/// - Robust to outliers (logarithmic compression)
/// - Penalizes under-predictions more heavily
/// - Requires non-negative values (uses 1 + x to handle zeros)
/// - Scale-independent for multiplicative relationships
/// </remarks>
[SkipLocalsInit]
public sealed class Msle : BiInputIndicatorBase
{
    /// <summary>
    /// Creates MSLE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Msle(int period) : base(period, $"Msle({period})") { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        // Ensure non-negative (MSLE requires non-negative values)
        double act = actual < 0 ? 0 : actual;
        double pred = predicted < 0 ? 0 : predicted;

        // MSLE formula: (log(1 + actual) - log(1 + predicted))²
        double logActual = Math.Log(1.0 + act);
        double logPredicted = Math.Log(1.0 + pred);
        double logError = logActual - logPredicted;
        return logError * logError;
    }

    /// <summary>
    /// Calculates MSLE for entire series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Batch calculation using log squared error computation with rolling mean.
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

        ComputeLogSquaredErrors(actual, predicted, errors);
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeLogSquaredErrors(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output)
    {
        int len = actual.Length;
        double lastValidActual = 0, lastValidPredicted = 0;

        // Find first valid non-negative values
        for (int i = 0; i < len; i++)
            if (double.IsFinite(actual[i]) && actual[i] >= 0) { lastValidActual = actual[i]; break; }
        for (int i = 0; i < len; i++)
            if (double.IsFinite(predicted[i]) && predicted[i] >= 0) { lastValidPredicted = predicted[i]; break; }

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            // Handle NaN/Infinity and negative values
            if (double.IsFinite(act) && act >= 0) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred) && pred >= 0) lastValidPredicted = pred; else pred = lastValidPredicted;

            double logActual = Math.Log(1.0 + act);
            double logPredicted = Math.Log(1.0 + pred);
            double logError = logActual - logPredicted;
            output[i] = logError * logError;
        }
    }
}
