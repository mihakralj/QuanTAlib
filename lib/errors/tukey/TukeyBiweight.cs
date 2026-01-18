using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TukeyBiweight: Tukey's Biweight (Bisquare) Loss
/// </summary>
/// <remarks>
/// Tukey's Biweight is a robust loss function that completely rejects outliers
/// beyond a threshold c. Unlike Huber loss which downweights outliers, Tukey's
/// biweight assigns zero weight to extreme outliers, making it highly resistant
/// to contaminated data.
///
/// Formula:
/// ρ(x) = (c²/6) * (1 - (1 - (x/c)²)³)  for |x| ≤ c
/// ρ(x) = c²/6                           for |x| > c
///
/// Key properties:
/// - Completely rejects outliers beyond threshold c
/// - Redescending: influence function goes to zero for large errors
/// - Common c values: 4.685 (95% efficiency), 6.0 (more permissive)
/// - More robust than Huber for heavily contaminated data
/// - Smooth and differentiable everywhere
/// </remarks>
[SkipLocalsInit]
public sealed class TukeyBiweight : BiInputIndicatorBase
{
    private readonly double _cSquaredOver6;
    private const double DefaultC = 4.685; // 95% efficiency for normal distribution

    public TukeyBiweight(int period, double c = DefaultC)
        : base(period, $"TukeyBiweight({period},{c:F3})")
    {
        if (c <= 0)
            throw new ArgumentException("Threshold c must be positive", nameof(c));

        C = c;
        _cSquaredOver6 = (c * c) / 6.0;
    }

    public double C { get; }

    /// <summary>
    /// Computes Tukey's biweight loss for the error between actual and predicted values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double error = actual - predicted;
        double absError = Math.Abs(error);

        if (absError > C)
            return _cSquaredOver6;

        double ratio = error / C;
        double ratioSq = ratio * ratio;
        double oneMinusRatioSq = 1.0 - ratioSq;
        double cubed = oneMinusRatioSq * oneMinusRatioSq * oneMinusRatioSq;
        return _cSquaredOver6 * (1.0 - cubed);
    }

    public static TSeries Calculate(TSeries actual, TSeries predicted, int period, double c = DefaultC)
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

        Batch(actual.Values, predicted.Values, vSpan, period, c);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period, double c = DefaultC)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (c <= 0)
            throw new ArgumentException("Threshold c must be positive", nameof(c));

        int len = actual.Length;
        if (len == 0) return;

        // Rent buffer for intermediate Tukey biweight errors
        double[] rented = ArrayPool<double>.Shared.Rent(len);
        try
        {
            Span<double> errors = rented.AsSpan(0, len);

            // Step 1: Compute Tukey biweight errors using ErrorHelpers
            ErrorHelpers.ComputeTukeyBiweightErrors(actual, predicted, errors, c);

            // Step 2: Apply rolling mean
            ErrorHelpers.ApplyRollingMean(errors, output, period, ResyncInterval);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rented, clearArray: false);
        }
    }
}