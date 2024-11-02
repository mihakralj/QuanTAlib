using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Curvature: Second Derivative Rate of Change
/// A statistical measure that calculates the rate of change of the slope over time.
/// Curvature provides insights into trend acceleration or deceleration by measuring
/// how quickly the slope (first derivative) is changing.
/// </summary>
/// <remarks>
/// The Curvature calculation process:
/// 1. Calculates slope values over the specified period
/// 2. Applies least squares regression to slope values
/// 3. Provides slope of slopes (curvature)
/// 4. Includes additional statistical measures (R², StdDev)
///
/// Key characteristics:
/// - Measures trend acceleration/deceleration
/// - Positive values indicate accelerating uptrends or decelerating downtrends
/// - Negative values indicate decelerating uptrends or accelerating downtrends
/// - Helps identify potential trend reversals
/// - Provides trend momentum information
///
/// Formula:
/// Curvature = Σ((x - x̄)(y - ȳ)) / Σ((x - x̄)²)
/// where:
/// x = time points
/// y = slope values
/// x̄, ȳ = respective means
///
/// Sources:
///     https://en.wikipedia.org/wiki/Curvature
///     https://www.sciencedirect.com/topics/mathematics/curve-fitting
///
/// Note: Second-order derivative providing acceleration insights
/// </remarks>

[SkipLocalsInit]
public sealed class Curvature : AbstractBase
{
    private readonly int _period;
    private readonly Slope _slopeCalculator;
    private readonly CircularBuffer _slopeBuffer;
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Gets the y-intercept of the curvature line.
    /// </summary>
    public double? Intercept { get; private set; }

    /// <summary>
    /// Gets the standard deviation of the slope values used in the curvature calculation.
    /// </summary>
    public double? StdDev { get; private set; }

    /// <summary>
    /// Gets the R-squared value, indicating the goodness of fit of the curvature line.
    /// </summary>
    public double? RSquared { get; private set; }

    /// <summary>
    /// Gets the last calculated point on the curvature line.
    /// </summary>
    public double? Line { get; private set; }

    /// <param name="period">The number of points to consider for calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is 2 or less.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Curvature(int period)
    {
        if (period <= 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period,
                "Period must be greater than 2 for Curvature calculation.");
        }
        _period = period;
        WarmupPeriod = period * 2 - 1; // Number of points needed for period number of slopes
        _slopeCalculator = new Slope(period);
        _slopeBuffer = new CircularBuffer(period);
        Name = $"Curvature(period={period})";

        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Curvature(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _slopeBuffer.Clear();
        Intercept = null;
        StdDev = null;
        RSquared = null;
        Line = null;
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
    private static (double sumX, double sumY) CalculateSums(ReadOnlySpan<double> slopes, int count)
    {
        double sumX = 0, sumY = 0;
        for (int i = 0; i < count; i++)
        {
            sumX += i + 1;
            sumY += slopes[i];
        }
        return (sumX, sumY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double sumSqX, double sumSqY, double sumSqXY) CalculateSquaredSums(
        ReadOnlySpan<double> slopes, int count, double avgX, double avgY)
    {
        double sumSqX = 0, sumSqY = 0, sumSqXY = 0;
        for (int i = 0; i < count; i++)
        {
            double devX = (i + 1) - avgX;
            double devY = slopes[i] - avgY;
            sumSqX += devX * devX;
            sumSqY += devY * devY;
            sumSqXY += devX * devY;
        }
        return (sumSqX, sumSqY, sumSqXY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        var slopeResult = _slopeCalculator.Calc(Input);
        _slopeBuffer.Add(slopeResult.Value, Input.IsNew);

        double curvature = 0;

        if (_slopeBuffer.Count < 2)
        {
            return curvature; // Not enough points for calculation
        }

        int count = Math.Min(_slopeBuffer.Count, _period);
        ReadOnlySpan<double> slopes = _slopeBuffer.GetSpan();

        // Calculate averages
        var (sumX, sumY) = CalculateSums(slopes, count);
        double avgX = sumX / count;
        double avgY = sumY / count;

        // Least squares method
        var (sumSqX, sumSqY, sumSqXY) = CalculateSquaredSums(slopes, count, avgX, avgY);

        if (sumSqX > Epsilon)
        {
            curvature = sumSqXY / sumSqX;
            Intercept = avgY - (curvature * avgX);

            // Calculate Standard Deviation and R-Squared
            double stdDevX = Math.Sqrt(sumSqX / count);
            double stdDevY = Math.Sqrt(sumSqY / count);
            StdDev = stdDevY;

            double stdDevProduct = stdDevX * stdDevY;
            if (stdDevProduct > Epsilon)
            {
                double r = sumSqXY / (stdDevProduct) / count;
                RSquared = r * r;
            }

            // Calculate last Line value (y = mx + b)
            Line = (curvature * count) + Intercept;
        }
        else
        {
            Intercept = null;
            StdDev = null;
            RSquared = null;
            Line = null;
        }

        IsHot = _slopeBuffer.Count == _period;
        return curvature;
    }
}
