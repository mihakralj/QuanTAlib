using System.Runtime.CompilerServices;
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

[SkipLocalsInit]
public sealed class Zscore : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for Z-score calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Zscore(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for Z-score calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _buffer = new CircularBuffer(period);
        Name = $"ZScore(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for Z-score calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Zscore(object source, int period) : this(period)
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
    private static double CalculateStandardDeviation(ReadOnlySpan<double> values, double mean)
    {
        double sumSquaredDeviations = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double deviation = values[i] - mean;
            sumSquaredDeviations += deviation * deviation;
        }
        return Math.Sqrt(sumSquaredDeviations / (values.Length - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double zScore = 0;
        if (_buffer.Count >= MinimumPoints)  // Need at least 2 points for standard deviation
        {
            ReadOnlySpan<double> values = _buffer.GetSpan();
            double mean = CalculateMean(values);
            double standardDeviation = CalculateStandardDeviation(values, mean);

            if (standardDeviation > Epsilon)  // Avoid division by zero
            {
                zScore = (Input.Value - mean) / standardDeviation;
            }
        }

        IsHot = _buffer.Count >= Period;
        return zScore;
    }
}
