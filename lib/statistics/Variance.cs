using System;
using System.Linq;
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

public class Variance : AbstractBase
{
    private readonly bool IsPopulation;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for variance calculation.</param>
    /// <param name="isPopulation">True for population variance, false for sample variance (default).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    public Variance(int period, bool isPopulation = false)
    {
        if (period < 2)
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
    public Variance(object source, int period, bool isPopulation = false) : this(period, isPopulation)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double variance = 0;
        if (_buffer.Count > 1)
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();

            // Calculate sum of squared deviations
            double sumOfSquaredDifferences = values.Sum(x => Math.Pow(x - mean, 2));

            // Use appropriate divisor based on population/sample calculation
            double divisor = IsPopulation ? _buffer.Count : _buffer.Count - 1;
            variance = sumOfSquaredDifferences / divisor;
        }

        IsHot = true;
        return variance;
    }
}
