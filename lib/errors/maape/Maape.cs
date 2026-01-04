using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MAAPE: Mean Arctangent Absolute Percentage Error
/// </summary>
/// <remarks>
/// MAAPE uses the arctangent function to bound the error between 0 and π/2,
/// making it more robust to outliers and handling zero actual values gracefully.
///
/// Formula:
/// MAAPE = (1/n) * Σ arctan(|actual - predicted| / |actual|)
///
/// Key properties:
/// - Bounded output: always between 0 and π/2 (≈1.5708)
/// - Handles zero actual values gracefully (approaches π/2)
/// - Less sensitive to outliers than MAPE
/// - Scale-independent
/// </remarks>
[SkipLocalsInit]
public sealed class Maape : BiInputIndicatorBase
{
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates a MAAPE (Mean Arctangent Absolute Percentage Error) indicator.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Maape(int period)
        : base(period, $"Maape({period})")
    {
    }

    /// <summary>
    /// Computes arctangent of percentage error: arctan(|error| / |actual|)
    /// Returns π/2 when actual is near zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double absActual = Math.Abs(actual);
        double absError = Math.Abs(actual - predicted);
        return absActual > Epsilon ? Math.Atan(absError / absActual) : Math.PI / 2.0;
    }

    /// <summary>
    /// Calculates Mean Arctangent Absolute Percentage Error for two time series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
    {
        if (actual.Count != predicted.Count)
            throw new ArgumentException("Actual and predicted series must have the same length", nameof(predicted));

        int len = actual.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(actual.Values, predicted.Values, vSpan, period);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation with O(1) rolling mean.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = actual.Length;
        if (len == 0) return;

        // Pre-compute arctangent errors
        const int StackAllocThreshold = 256;
        Span<double> errors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ComputeAtanErrors(actual, predicted, errors);

        // Apply rolling mean
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }

    /// <summary>
    /// Computes arctangent percentage errors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeAtanErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        int len = actual.Length;
        double lastValidActual = 0.0;
        double lastValidPredicted = 0.0;

        // Find first valid values
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k]))
            {
                lastValidActual = actual[k];
                break;
            }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(predicted[k]))
            {
                lastValidPredicted = predicted[k];
                break;
            }
        }

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
                lastValidActual = act;
            else
                act = lastValidActual;

            if (double.IsFinite(pred))
                lastValidPredicted = pred;
            else
                pred = lastValidPredicted;

            double absActual = Math.Abs(act);
            double absError = Math.Abs(act - pred);
            output[i] = absActual > Epsilon ? Math.Atan(absError / absActual) : Math.PI / 2.0;
        }
    }
}
