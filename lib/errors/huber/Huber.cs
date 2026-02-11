using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Huber: Huber Loss
/// </summary>
/// <remarks>
/// Huber Loss combines the best properties of MSE and MAE. For small errors
/// (|error| ≤ delta), it behaves like MSE (quadratic). For large errors
/// (|error| > delta), it behaves like MAE (linear).
///
/// Formula:
/// If |error| ≤ delta: L = 0.5 * error²
/// If |error| > delta:  L = delta * |error| - 0.5 * delta²
///
/// Key properties:
/// - Differentiable everywhere (unlike MAE)
/// - Robust to outliers (unlike MSE)
/// - Delta controls the transition point
/// - Default delta = 1.345 (for 95% efficiency with normal distribution)
/// </remarks>
[SkipLocalsInit]
public sealed class Huber : BiInputIndicatorBase
{
    private readonly double _negHalfDeltaSquared;

    /// <summary>
    /// Gets the delta parameter (transition threshold).
    /// </summary>
    public double Delta { get; }

    /// <summary>
    /// Creates a Huber Loss indicator.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    /// <param name="delta">Threshold for switching between quadratic and linear loss (must be > 0). Default 1.345</param>
    public Huber(int period, double delta = 1.345)
        : base(period, $"Huber({period},{delta:F3})")
    {
        if (delta <= 0)
        {
            throw new ArgumentException("Delta must be greater than 0", nameof(delta));
        }

        Delta = delta;
        _negHalfDeltaSquared = -0.5 * delta * delta;
    }

    /// <summary>
    /// Computes Huber loss for a single error value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double diff = actual - predicted;
        double absDiff = Math.Abs(diff);

        // Quadratic for small errors, linear for large errors
        // Linear: delta * |diff| - 0.5 * delta² = FMA(delta, |diff|, -0.5*delta²)
        return absDiff <= Delta
            ? 0.5 * diff * diff
            : Math.FusedMultiplyAdd(Delta, absDiff, _negHalfDeltaSquared);
    }

    /// <summary>
    /// Calculates Huber Loss for two time series.
    /// </summary>
    public static TSeries Batch(TSeries actual, TSeries predicted, int period, double delta = 1.345)
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

        Batch(actual.Values, predicted.Values, vSpan, period, delta);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation of Huber Loss using SIMD-accelerated error computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period, double delta = 1.345)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (delta <= 0)
        {
            throw new ArgumentException("Delta must be greater than 0", nameof(delta));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        // Pre-compute Huber errors using shared helper
        const int StackAllocThreshold = 256;
        Span<double> errors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ErrorHelpers.ComputeHuberErrors(actual, predicted, errors, delta);

        // Apply rolling mean
        ErrorHelpers.ApplyRollingMean(errors, output, period);
    }

    public static (TSeries Results, Huber Indicator) Calculate(TSeries actual, TSeries predicted, int period, double delta = 1.345)
    {
        var indicator = new Huber(period, delta);
        TSeries results = Batch(actual, predicted, period, delta);
        return (results, indicator);
    }
}