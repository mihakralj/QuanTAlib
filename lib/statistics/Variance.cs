using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VARIANCE: Squared Deviation Risk Measure
/// A statistical measure that quantifies the spread of data points around their
/// mean value. Variance is fundamental to risk assessment and portfolio theory,
/// providing the basis for many financial models.
/// </summary>
/// <remarks>
/// The Variance calculation process:
/// 1. Calculates mean of the data
/// 2. Computes squared deviations from mean
/// 3. Sums squared deviations
/// 4. Divides by n or (n-1)
///
/// Key characteristics:
/// - Measures data dispersion
/// - Squared units of input data
/// - Always non-negative
/// - Population or sample versions
/// - Foundation for risk metrics
///
/// Formula:
/// Population: σ² = Σ(x - μ)² / N
/// Sample: s² = Σ(x - x̄)² / (n-1)
/// where:
/// x = values
/// μ, x̄ = mean
/// N, n = count
///
/// Market Applications:
/// - Portfolio optimization
/// - Risk measurement
/// - Modern Portfolio Theory
/// - Asset allocation
/// - Volatility analysis
///
/// Sources:
///     Harry Markowitz - "Portfolio Selection" (1952)
///     https://en.wikipedia.org/wiki/Variance
///
/// Note: Basis for Modern Portfolio Theory and risk models
/// </remarks>
[SkipLocalsInit]
public sealed class Variance : AbstractBase
{
    private readonly bool IsPopulation;
    private readonly CircularBuffer _buffer;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for variance calculation.</param>
    /// <param name="isPopulation">True for population variance, false for sample variance (default).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Variance(int period, bool isPopulation = false)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2.");
        }
        IsPopulation = isPopulation;
        WarmupPeriod = 0;
        _buffer = new CircularBuffer(period);
        Name = $"Variance(period={period}, population={isPopulation})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for variance calculation.</param>
    /// <param name="isPopulation">True for population variance, false for sample variance (default).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Variance(object source, int period, bool isPopulation = false) : this(period, isPopulation)
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
    private static double CalculateSumSquaredDeviations(ReadOnlySpan<double> values, double mean)
    {
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            sum += diff * diff;
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double variance = 0;
        if (_buffer.Count > 1)
        {
            ReadOnlySpan<double> values = _buffer.GetSpan();
            double mean = CalculateMean(values);
            double sumOfSquaredDifferences = CalculateSumSquaredDeviations(values, mean);

            // Use appropriate divisor based on population/sample calculation
            double divisor = IsPopulation ? _buffer.Count : _buffer.Count - 1;
            variance = sumOfSquaredDifferences / divisor;
        }

        IsHot = true;
        return variance;
    }
}
