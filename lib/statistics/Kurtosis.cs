using System;
using System.Linq;
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

public class Kurtosis : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for kurtosis calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 4.</exception>
    public Kurtosis(int period)
    {
        if (period < 4)
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
    public Kurtosis(object source, int period) : this(period)
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

        double kurtosis = 0;
        if (_buffer.Count > 3)  // Need at least 4 points for valid calculation
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            // Calculate squared and fourth power deviations
            double s2 = 0;  // Sum of squared deviations
            double s4 = 0;  // Sum of fourth power deviations

            for (int i = 0; i < values.Length; i++)
            {
                double diff = values[i] - mean;
                s2 += diff * diff;
                s4 += diff * diff * diff * diff;
            }

            double variance = s2 / (n - 1);

            // Sheskin Algorithm for excess kurtosis
            kurtosis = (n * (n + 1) * s4) / (variance * variance * (n - 3) * (n - 1) * (n - 2))
                       - (3 * (n - 1) * (n - 1) / ((n - 2) * (n - 3)));
        }

        IsHot = _buffer.Count >= Period;
        return kurtosis;
    }
}
