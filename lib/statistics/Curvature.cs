namespace QuanTAlib;

/// <summary>
/// Calculates the rate of change of the slope over a specified period.
/// Provides insights into trend acceleration or deceleration.
/// </summary>
/// <remarks>
/// Curvature is a second-order derivative that measures how quickly the slope (first-order derivative) is changing.
/// Positive curvature indicates accelerating uptrends or decelerating downtrends.
/// Negative curvature indicates decelerating uptrends or accelerating downtrends.
/// This indicator can be useful for identifying potential trend reversals or confirming trend strength.
/// </remarks>
public class Curvature : AbstractBase
{
    private readonly int _period;
    private readonly Slope _slopeCalculator;
    private readonly CircularBuffer _slopeBuffer;

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

    /// <summary>
    /// Initializes a new instance of the Curvature class.
    /// </summary>
    /// <param name="period">The number of data points to consider for calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the period is 2 or less.
    /// </exception>
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

    /// <summary>
    /// Initializes a new instance of the Curvature class with a data source.
    /// </summary>
    /// <param name="source">The source object that publishes data.</param>
    /// <param name="period">The number of data points to consider.</param>
    public Curvature(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Resets the Curvature indicator to its initial state.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _slopeBuffer.Clear();
        Intercept = null;
        StdDev = null;
        RSquared = null;
        Line = null;
    }

    /// <summary>
    /// Manages the state of the indicator.
    /// </summary>
    /// <param name="isNew">Indicates if the current data point is new.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the curvature calculation.
    /// </summary>
    /// <returns>
    /// The calculated curvature value. Positive for increasing slope, negative for decreasing.
    /// </returns>
    /// <remarks>
    /// Uses least squares method for optimal calculation. Also computes additional statistics
    /// such as Intercept, Standard Deviation, R-Squared, and Line value.
    /// </remarks>
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
        var slopes = _slopeBuffer.GetSpan().ToArray();

        // Calculate averages
        double sumX = 0, sumY = 0;
        for (int i = 0; i < count; i++)
        {
            sumX += i + 1;
            sumY += slopes[i];
        }
        double avgX = sumX / count;
        double avgY = sumY / count;

        // Least squares method
        double sumSqX = 0, sumSqY = 0, sumSqXY = 0;
        for (int i = 0; i < count; i++)
        {
            double devX = (i + 1) - avgX;
            double devY = slopes[i] - avgY;
            sumSqX += devX * devX;
            sumSqY += devY * devY;
            sumSqXY += devX * devY;
        }

        if (sumSqX > 0)
        {
            curvature = sumSqXY / sumSqX;
            Intercept = avgY - (curvature * avgX);

            // Calculate Standard Deviation and R-Squared
            double stdDevX = Math.Sqrt(sumSqX / count);
            double stdDevY = Math.Sqrt(sumSqY / count);
            StdDev = stdDevY;

            if (stdDevX * stdDevY != 0)
            {
                double r = sumSqXY / (stdDevX * stdDevY) / count;
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
