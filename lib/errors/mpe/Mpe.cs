using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MPE: Mean Percentage Error
/// </summary>
/// <remarks>
/// MPE measures the average percentage error between actual and predicted values,
/// preserving the sign to detect directional bias. Unlike MAPE, it can reveal
/// systematic over- or under-prediction.
///
/// Formula:
/// MPE = (100/n) * Σ((actual - predicted) / actual)
///
/// Key properties:
/// - Scale-independent (expressed as percentage)
/// - Preserves sign: positive = under-prediction, negative = over-prediction
/// - Cannot be calculated when actual = 0
/// - Useful for detecting systematic bias in predictions
/// </remarks>
[SkipLocalsInit]
public sealed class Mpe : BiInputIndicatorBase
{
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates MPE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mpe(int period) : base(period, $"Mpe({period})") { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        // MPE: 100 * (actual - predicted) / actual (preserves sign)
        double divisor = Math.Abs(actual) < Epsilon ? Epsilon : actual;
        return 100.0 * (actual - predicted) / divisor;
    }

    /// <summary>
    /// Calculates MPE for entire series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
        => CalculateImpl(actual, predicted, period, Batch);

    /// <summary>
    /// Batch calculation using signed percentage error computation with rolling mean.
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

        ComputeSignedPercentageErrors(actual, predicted, errors);
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSignedPercentageErrors(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output)
    {
        int len = actual.Length;
        double lastValidActual = 1.0, lastValidPredicted = 0;

        for (int i = 0; i < len; i++)
            if (double.IsFinite(actual[i]) && Math.Abs(actual[i]) >= Epsilon) { lastValidActual = actual[i]; break; }
        for (int i = 0; i < len; i++)
            if (double.IsFinite(predicted[i])) { lastValidPredicted = predicted[i]; break; }

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act) && Math.Abs(act) >= Epsilon) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double divisor = Math.Abs(act) < Epsilon ? Epsilon : act;
            output[i] = 100.0 * (act - pred) / divisor;
        }
    }
}
