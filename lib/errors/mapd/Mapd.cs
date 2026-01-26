using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MAPD: Mean Absolute Percentage Deviation
/// </summary>
/// <remarks>
/// MAPD measures the average absolute percentage deviation between actual and predicted values.
/// Unlike MAPE which divides by actual, MAPD divides by predicted.
///
/// Formula:
/// MAPD = (100/n) * Σ|((actual - predicted) / predicted)|
///
/// Key properties:
/// - Scale-independent (expressed as percentage)
/// - Cannot be calculated when predicted = 0 (uses epsilon protection)
/// - Differs from MAPE in denominator choice
/// - More stable when actuals have high variance
/// </remarks>
[SkipLocalsInit]
public sealed class Mapd : BiInputIndicatorBase
{
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates a MAPD (Mean Absolute Percentage Deviation) indicator.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mapd(int period)
        : base(period, $"Mapd({period})")
    {
    }

    /// <summary>
    /// Computes percentage deviation: |actual - predicted| / |predicted| * 100
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double absPredicted = Math.Abs(predicted);
        return absPredicted > Epsilon
            ? Math.Abs(actual - predicted) / absPredicted * 100.0
            : 0.0;
    }

    /// <summary>
    /// Calculates Mean Absolute Percentage Deviation for two time series.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
    {
        if (actual.Count != predicted.Count)
        {
            throw new ArgumentException("Actual and predicted series must have the same length", nameof(predicted));
        }

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
    /// Batch computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        // Pre-compute percentage errors (divided by predicted, not actual)
        const int StackAllocThreshold = 256;
        Span<double> errors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ComputeMapdErrors(actual, predicted, errors);

        // Apply rolling mean
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }

    /// <summary>
    /// Computes MAPD errors (percentage errors divided by predicted).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeMapdErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        int len = actual.Length;
        double lastValidActual = 0.0;
        double lastValidPredicted = 1.0; // Default to 1 to avoid division by zero

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
            if (double.IsFinite(predicted[k]) && Math.Abs(predicted[k]) >= Epsilon)
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
            {
                lastValidActual = act;
            }
            else
            {
                act = lastValidActual;
            }

            if (double.IsFinite(pred) && Math.Abs(pred) >= Epsilon)
            {
                lastValidPredicted = pred;
            }
            else
            {
                pred = lastValidPredicted;
            }

            double absPredicted = Math.Abs(pred);
            output[i] = absPredicted > Epsilon
                ? Math.Abs(act - pred) / absPredicted * 100.0
                : 0.0;
        }
    }
}
