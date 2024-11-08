using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CORR: Correlation Coefficient
/// A statistical measure that quantifies the strength and direction of the relationship
/// between two variables. The correlation coefficient ranges from -1 to 1, where 1 indicates
/// a perfect positive correlation, -1 indicates a perfect negative correlation, and 0 indicates
/// no correlation.
/// </summary>
/// <remarks>
/// The Correlation calculation process:
/// 1. Calculates mean of both variables
/// 2. Computes covariance between variables
/// 3. Calculates standard deviation of both variables
/// 4. Divides covariance by product of standard deviations
///
/// Key characteristics:
/// - Measures linear relationship strength
/// - Symmetric around zero
/// - Scale-independent measure
/// - Sensitive to outliers
/// - Useful for portfolio diversification
///
/// Formula:
/// ρ = Cov(X, Y) / (σX * σY)
/// where:
/// X, Y = variables
/// Cov = covariance
/// σ = standard deviation
///
/// Market Applications:
/// - Portfolio diversification
/// - Risk management
/// - Pairs trading
/// - Performance analysis
/// - Market sentiment analysis
///
/// Sources:
///     https://en.wikipedia.org/wiki/Correlation_coefficient
///     "Modern Portfolio Theory" - Harry Markowitz
///
/// Note: Assumes linear relationship between variables
/// </remarks>
[SkipLocalsInit]
public sealed class Corr : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _xValues;
    private readonly CircularBuffer _yValues;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for correlation calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Corr(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for correlation calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _xValues = new CircularBuffer(period);
        _yValues = new CircularBuffer(period);
        Name = $"Corr(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for correlation calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Corr(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _xValues.Clear();
        _yValues.Clear();
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
    private static double CalculateCovariance(ReadOnlySpan<double> xValues, ReadOnlySpan<double> yValues, double xMean, double yMean)
    {
        double covariance = 0;
        for (int i = 0; i < xValues.Length; i++)
        {
            covariance += (xValues[i] - xMean) * (yValues[i] - yMean);
        }
        return covariance / xValues.Length;
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
        return Math.Sqrt(sumSquaredDeviations / values.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _xValues.Add(Input.Value, Input.IsNew);
        _yValues.Add(Input2.Value, Input.IsNew);

        double correlation = 0;
        if (_xValues.Count >= MinimumPoints && _yValues.Count >= MinimumPoints)
        {
            ReadOnlySpan<double> xValues = _xValues.GetSpan();
            ReadOnlySpan<double> yValues = _yValues.GetSpan();

            double xMean = CalculateMean(xValues);
            double yMean = CalculateMean(yValues);

            double covariance = CalculateCovariance(xValues, yValues, xMean, yMean);
            double xStdDev = CalculateStandardDeviation(xValues, xMean);
            double yStdDev = CalculateStandardDeviation(yValues, yMean);

            if (xStdDev > Epsilon && yStdDev > Epsilon)
            {
                correlation = covariance / (xStdDev * yStdDev);
            }
        }

        IsHot = _xValues.Count >= Period && _yValues.Count >= Period;
        return correlation;
    }
}
