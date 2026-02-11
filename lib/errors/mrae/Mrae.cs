using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MRAE: Mean Relative Absolute Error
/// </summary>
/// <remarks>
/// MRAE measures the average relative absolute error, normalizing each error
/// by the absolute actual value. Similar to MAPE but expressed as a ratio (0-1)
/// rather than percentage (0-100%).
///
/// Formula:
/// MRAE = (1/n) * Σ(|actual - predicted| / |actual|)
///
/// Key properties:
/// - Scale-independent through normalization
/// - Values typically between 0 and 1 (0 = perfect, 1 = 100% error)
/// - Undefined when actual = 0 (uses epsilon protection)
/// - Equivalent to MAPE / 100
/// </remarks>
[SkipLocalsInit]
public sealed class Mrae : BiInputIndicatorBase
{
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates a MRAE (Mean Relative Absolute Error) indicator.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mrae(int period)
        : base(period, $"Mrae({period})")
    {
    }

    /// <summary>
    /// Computes relative absolute error: |actual - predicted| / |actual|
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double absActual = Math.Abs(actual);
        return absActual > Epsilon
            ? Math.Abs(actual - predicted) / absActual
            : 0.0;
    }

    /// <summary>
    /// Calculates Mean Relative Absolute Error for two time series.
    /// </summary>
    public static TSeries Batch(TSeries actual, TSeries predicted, int period)
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
    /// Batch computation using shared error helpers.
    /// </summary>
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

        // Pre-compute relative errors (same as percentage errors but without *100)
        const int StackAllocThreshold = 256;
        Span<double> errors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ComputeRelativeErrors(actual, predicted, errors);

        // Apply rolling mean
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }

    public static (TSeries Results, Mrae Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Mrae(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }

    /// <summary>
    /// Computes relative errors (0-1 scale, not percentage).
    /// </summary>
    private static void ComputeRelativeErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        int len = actual.Length;
        double lastValidActual = 1.0;
        double lastValidPredicted = 0.0;

        // Find first valid values
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k]) && Math.Abs(actual[k]) >= Epsilon)
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

            if (double.IsFinite(act) && Math.Abs(act) >= Epsilon)
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

            double absActual = Math.Abs(act);
            output[i] = absActual > Epsilon
                ? Math.Abs(act - pred) / absActual
                : 0.0;
        }
    }
}