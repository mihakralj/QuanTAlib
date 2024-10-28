using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Kurtosis: Distribution Tail Weight Measure
/// A statistical measure that quantifies the "tailedness" of a distribution using
/// the Sheskin Algorithm. Kurtosis indicates whether data has heavy tails (more
/// outliers) or light tails (fewer outliers) compared to a normal distribution.
/// </summary>
/// <remarks>
/// The Kurtosis calculation process:
/// 1. Calculates mean of the data
/// 2. Computes squared and fourth power deviations
/// 3. Applies Sheskin Algorithm for excess kurtosis
/// 4. Adjusts for sample size bias
///
/// Key characteristics:
/// - Measures tail weight relative to normal distribution
/// - Positive values indicate heavy tails
/// - Negative values indicate light tails
/// - Zero indicates normal distribution
/// - Sensitive to extreme values
///
/// Formula:
/// K = [n(n+1)Σ(x-μ)⁴] / [s⁴(n-1)(n-2)(n-3)] - [3(n-1)²]/[(n-2)(n-3)]
/// where:
/// n = sample size
/// μ = mean
/// s = standard deviation
///
/// Market Applications:
/// - Identify potential for extreme moves
/// - Assess risk of "black swan" events
/// - Compare return distributions
/// - Risk management tool
///
/// Sources:
///     David J. Sheskin - "Handbook of Parametric and Nonparametric Statistical Procedures"
///     https://en.wikipedia.org/wiki/Kurtosis
///
/// Note: Returns excess kurtosis (normal distribution = 0)
/// </remarks>

[SkipLocalsInit]
public sealed class Kurtosis : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 4;

    /// <param name="period">The number of points to consider for kurtosis calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 4.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Kurtosis(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 4 for kurtosis calculation.");
        }
        Period = period;
        WarmupPeriod = Period - 1;
        _buffer = new CircularBuffer(period);
        Name = $"Kurtosis(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for kurtosis calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Kurtosis(object source, int period) : this(period)
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
    private static (double s2, double s4) CalculateDeviations(ReadOnlySpan<double> values, double mean)
    {
        double s2 = 0;  // Sum of squared deviations
        double s4 = 0;  // Sum of fourth power deviations

        for (int i = 0; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            double diff2 = diff * diff;
            s2 += diff2;
            s4 += diff2 * diff2;
        }

        return (s2, s4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateSheskinKurtosis(double s2, double s4, int n)
    {
        double variance = s2 / (n - 1);
        double variance2 = variance * variance;

        if (variance2 < Epsilon)
            return 0;

        return (n * (n + 1) * s4) / (variance2 * (n - 3) * (n - 1) * (n - 2))
               - (3 * (n - 1) * (n - 1) / ((n - 2) * (n - 3)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        double kurtosis = 0;
        if (_buffer.Count > MinimumPoints - 1)  // Need at least 4 points for valid calculation
        {
            ReadOnlySpan<double> values = _buffer.GetSpan();
            double mean = CalculateMean(values);
            var (s2, s4) = CalculateDeviations(values, mean);
            kurtosis = CalculateSheskinKurtosis(s2, s4, values.Length);
        }

        IsHot = _buffer.Count >= Period;
        return kurtosis;
    }
}
