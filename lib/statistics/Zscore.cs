using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// ZSCORE: Standardized Distance Measure
/// A statistical measure that indicates how many standard deviations an observation
/// is from the mean. Z-scores normalize data to a standard scale, making it useful
/// for comparing values across different distributions.
/// </summary>
/// <remarks>
/// The Zscore calculation process:
/// 1. Calculates mean of the period
/// 2. Computes standard deviation
/// 3. Measures distance from mean
/// 4. Normalizes by standard deviation
///
/// Key characteristics:
/// - Scale-independent measure
/// - Symmetric around zero
/// - Normal distribution context
/// - Outlier identification
/// - Comparative analysis tool
///
/// Formula:
/// Z = (x - μ) / σ
/// where:
/// x = current value
/// μ = mean
/// σ = standard deviation
///
/// Market Applications:
/// - Mean reversion strategies
/// - Overbought/oversold signals
/// - Volatility breakouts
/// - Cross-asset comparison
/// - Statistical arbitrage
///
/// Sources:
///     https://en.wikipedia.org/wiki/Standard_score
///     "Statistical Analysis in Trading" - Technical Analysis
///
/// Note: Assumes approximately normal distribution
/// </remarks>

public class Zscore : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for Z-score calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    public Zscore(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for Z-score calculation.");
        }
        Period = period;
        WarmupPeriod = 2;
        _buffer = new CircularBuffer(period);
        Name = $"ZScore(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for Z-score calculation.</param>
    public Zscore(object source, int period) : this(period)
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

        double zScore = 0;
        if (_buffer.Count >= 2)  // Need at least 2 points for standard deviation
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            // Calculate sample standard deviation
            double sumSquaredDeviations = values.Sum(x => Math.Pow(x - mean, 2));
            double standardDeviation = Math.Sqrt(sumSquaredDeviations / (n - 1));

            if (standardDeviation != 0)  // Avoid division by zero
            {
                zScore = (Input.Value - mean) / standardDeviation;
            }
        }

        IsHot = _buffer.Count >= Period;
        return zScore;
    }
}
