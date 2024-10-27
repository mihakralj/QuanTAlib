using System;
using System.Linq;
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

public class Skew : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for skewness calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 3.</exception>
    public Skew(int period)
    {
        if (period < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 3 for skewness calculation.");
        }
        Period = period;
        WarmupPeriod = 3;
        _buffer = new CircularBuffer(period);
        Name = $"Skew(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for skewness calculation.</param>
    public Skew(object source, int period) : this(period)
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

        double skew = 0;
        if (_buffer.Count >= 3)  // Need at least 3 points for skewness
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            // Calculate third and second moments
            double sumCubedDeviations = 0;
            double sumSquaredDeviations = 0;

            foreach (var value in values)
            {
                double deviation = value - mean;
                sumCubedDeviations += Math.Pow(deviation, 3);
                sumSquaredDeviations += Math.Pow(deviation, 2);
            }

            // Fisher-Pearson standardized moment coefficient
            double m3 = sumCubedDeviations / n;
            double m2 = sumSquaredDeviations / n;
            double s3 = Math.Pow(m2, 1.5);

            if (s3 != 0)  // Avoid division by zero
            {
                skew = (Math.Sqrt(n * (n - 1)) / (n - 2)) * (m3 / s3);
            }
        }

        IsHot = _buffer.Count >= Period;
        return skew;
    }
}
