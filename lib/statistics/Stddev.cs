using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// STDDEV: Standard Deviation Volatility Measure
/// A statistical measure that quantifies the amount of variation or dispersion
/// in a dataset. Standard deviation is widely used in finance as a measure of
/// volatility and risk assessment.
/// </summary>
/// <remarks>
/// The StdDev calculation process:
/// 1. Calculates mean of the data
/// 2. Computes squared deviations from mean
/// 3. Averages squared deviations
/// 4. Takes square root of average
///
/// Key characteristics:
/// - Measures data dispersion
/// - Same units as input data
/// - Sensitive to outliers
/// - Population or sample versions
/// - Key volatility indicator
///
/// Formula:
/// Population: σ = √(Σ(x - μ)² / N)
/// Sample: s = √(Σ(x - x̄)² / (n-1))
/// where:
/// x = values
/// μ, x̄ = mean
/// N, n = count
///
/// Market Applications:
/// - Volatility measurement
/// - Risk assessment
/// - Bollinger Bands
/// - Option pricing
/// - Portfolio management
///
/// Sources:
///     https://en.wikipedia.org/wiki/Standard_deviation
///     "Options, Futures, and Other Derivatives" - John C. Hull
///
/// Note: Foundation for many volatility-based indicators
/// </remarks>

public class Stddev : AbstractBase
{
    private readonly bool IsPopulation;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for standard deviation calculation.</param>
    /// <param name="isPopulation">True for population stddev, false for sample stddev (default).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    public Stddev(int period, bool isPopulation = false)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2.");
        }
        IsPopulation = isPopulation;
        WarmupPeriod = 0;
        _buffer = new CircularBuffer(period);
        Name = $"Stddev(period={period}, population={isPopulation})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for standard deviation calculation.</param>
    /// <param name="isPopulation">True for population stddev, false for sample stddev (default).</param>
    public Stddev(object source, int period, bool isPopulation = false) : this(period, isPopulation)
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

        double stddev = 0;
        if (_buffer.Count > 1)
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();

            // Calculate sum of squared deviations
            double sumOfSquaredDifferences = values.Sum(x => Math.Pow(x - mean, 2));

            // Use appropriate divisor based on population/sample calculation
            double divisor = IsPopulation ? _buffer.Count : _buffer.Count - 1;
            double variance = sumOfSquaredDifferences / divisor;
            stddev = Math.Sqrt(variance);
        }

        IsHot = true; // StdDev calc is valid from bar 1
        return stddev;
    }
}
