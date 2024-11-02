using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// SKEW: Distribution Asymmetry Measure
/// A statistical measure that quantifies the asymmetry of a probability distribution
/// around its mean. Skewness indicates whether deviations from the mean are more
/// likely in one direction than the other.
/// </summary>
/// <remarks>
/// The Skew calculation process:
/// 1. Calculates mean of the data
/// 2. Computes deviations from mean
/// 3. Calculates third moment (cubed deviations)
/// 4. Normalizes by standard deviation cubed
///
/// Key characteristics:
/// - Measures distribution asymmetry
/// - Positive values indicate right skew
/// - Negative values indicate left skew
/// - Zero indicates symmetry
/// - Scale-independent measure
///
/// Formula:
/// skew = [√(n(n-1))/(n-2)] * [m₃/s³]
/// where:
/// m₃ = third moment about the mean
/// s = standard deviation
/// n = sample size
///
/// Market Applications:
/// - Risk assessment in returns
/// - Options pricing models
/// - Trading strategy development
/// - Portfolio risk management
/// - Market sentiment analysis
///
/// Sources:
///     Fisher-Pearson standardized moment coefficient
///     https://en.wikipedia.org/wiki/Skewness
///     "The Analysis of Financial Time Series" - Ruey S. Tsay
///
/// Note: Requires minimum of 3 data points for calculation
/// </remarks>

[SkipLocalsInit]
public sealed class Skew : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 3;

    /// <param name="period">The number of points to consider for skewness calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 3.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Skew(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 3 for skewness calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _buffer = new CircularBuffer(period);
        Name = $"Skew(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for skewness calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Skew(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateMean(ReadOnlySpan<double> values)
    {
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        return sum / values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double m3, double m2) CalculateMoments(ReadOnlySpan<double> values, double mean)
    {
        double sumCubedDeviations = 0;
        double sumSquaredDeviations = 0;

        for (int i = 0; i < values.Length; i++)
        {
            double deviation = values[i] - mean;
            double squared = deviation * deviation;
            sumSquaredDeviations += squared;
            sumCubedDeviations += squared * deviation;
        }

        double n = values.Length;
        return (sumCubedDeviations / n, sumSquaredDeviations / n);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateSkewness(double m3, double m2, int n)
    {
        double s3 = Math.Pow(m2, 1.5);
        if (s3 < Epsilon)
            return 0;

        return (Math.Sqrt(n * (n - 1)) / (n - 2)) * (m3 / s3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double skew = 0;
        if (_buffer.Count >= MinimumPoints)  // Need at least 3 points for skewness
        {
            ReadOnlySpan<double> values = _buffer.GetSpan();
            double mean = CalculateMean(values);
            var (m3, m2) = CalculateMoments(values, mean);
            skew = CalculateSkewness(m3, m2, values.Length);
        }

        IsHot = _buffer.Count >= Period;
        return skew;
    }
}
