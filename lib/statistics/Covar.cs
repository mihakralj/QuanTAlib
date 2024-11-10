using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// COVAR: Covariance
/// A statistical measure that quantifies how two variables change together. Unlike correlation,
/// covariance is not normalized and therefore is scale-dependent. A positive covariance indicates
/// that variables tend to move in the same direction, while a negative covariance indicates
/// opposite movement.
/// </summary>
/// <remarks>
/// The Covariance calculation process:
/// 1. Calculates mean of both variables
/// 2. For each pair of points, multiply their deviations from their respective means
/// 3. Sum these products and divide by the number of observations
///
/// Key characteristics:
/// - Measures linear relationship
/// - Scale-dependent measure
/// - Sign indicates direction of relationship
/// - Magnitude depends on scale of variables
/// - Basis for correlation coefficient
///
/// Formula:
/// Cov(X,Y) = Σ((x - μx)(y - μy)) / n
/// where:
/// X, Y = variables
/// μx, μy = means of X and Y
/// n = number of observations
///
/// Market Applications:
/// - Portfolio risk analysis
/// - Pairs trading strategy development
/// - Asset relationship analysis
/// - Risk factor sensitivity analysis
/// - Multi-asset portfolio optimization
///
/// Sources:
///     https://en.wikipedia.org/wiki/Covariance
///     "Modern Portfolio Theory" - Harry Markowitz
///
/// Note: Scale-dependent nature means values should be interpreted in context of the data scales
/// </remarks>
[SkipLocalsInit]
public sealed class Covar : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _xValues;
    private readonly CircularBuffer _yValues;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for covariance calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Covar(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for covariance calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _xValues = new CircularBuffer(period);
        _yValues = new CircularBuffer(period);
        Name = $"Covar(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for covariance calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Covar(object source, int period) : this(period)
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
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _xValues.Add(Input.Value, Input.IsNew);
        _yValues.Add(Input2.Value, Input.IsNew);

        double covariance = 0;
        if (_xValues.Count >= MinimumPoints && _yValues.Count >= MinimumPoints)
        {
            ReadOnlySpan<double> xValues = _xValues.GetSpan();
            ReadOnlySpan<double> yValues = _yValues.GetSpan();

            double xMean = CalculateMean(xValues);
            double yMean = CalculateMean(yValues);

            covariance = CalculateCovariance(xValues, yValues, xMean, yMean);
        }

        IsHot = _xValues.Count >= Period && _yValues.Count >= Period;
        return covariance;
    }
}
