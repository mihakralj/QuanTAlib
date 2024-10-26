namespace QuanTAlib;

/// <summary>
/// Represents a slope calculator that performs linear regression on a series of data points.
/// </summary>
/// <remarks>
/// The Slope class calculates the slope of a linear regression line, along with other
/// statistical measures such as intercept, standard deviation, R-squared, and the last
/// point on the regression line. It uses the least squares method for calculation.
///
/// In financial analysis, slope is important for:
/// - Identifying trends in price movements or other financial metrics.
/// - Measuring the rate of change in a financial time series.
/// - Assessing the strength and direction of relationships between variables.
/// - Supporting technical analysis indicators and trading strategies.
/// </remarks>
public class Slope : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _buffer;
    private readonly CircularBuffer _timeBuffer;

    /// <summary>
    /// Gets the y-intercept of the regression line.
    /// </summary>
    public double? Intercept { get; private set; }

    /// <summary>
    /// Gets the standard deviation of the y-values.
    /// </summary>
    public double? StdDev { get; private set; }

    /// <summary>
    /// Gets the R-squared value, indicating the goodness of fit of the regression line.
    /// </summary>
    public double? RSquared { get; private set; }

    /// <summary>
    /// Gets the y-value of the last point on the regression line.
    /// </summary>
    public double? Line { get; private set; }

    /// <summary>
    /// Initializes a new instance of the Slope class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the slope.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than or equal to 1.
    /// </exception>
    public Slope(int period)
    {
        if (period <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period,
                "Period must be greater than 1 for Slope/Linear Regression.");
        }
        _period = period;
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        _timeBuffer = new CircularBuffer(period);
        Name = $"Slope(period={period})";

        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Slope class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the slope.</param>
    public Slope(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Slope instance by clearing buffers and resetting calculated values.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _timeBuffer.Clear();
        Intercept = null;
        StdDev = null;
        RSquared = null;
        Line = null;
    }

    /// <summary>
    /// Manages the state of the Slope instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the slope calculation using linear regression for the current period.
    /// </summary>
    /// <returns>
    /// The calculated slope value for the current period.
    /// </returns>
    /// <remarks>
    /// This method uses the least squares method to calculate the slope of the regression line.
    /// It also calculates and updates the Intercept, StdDev, RSquared, and Line properties.
    /// If there are fewer than 2 data points, or if the sum of squared x deviations is 0,
    /// the method returns 0 and sets the additional properties to null.
    ///
    /// Interpretation of results:
    /// - Positive slope: Indicates an upward trend in the data.
    /// - Negative slope: Indicates a downward trend in the data.
    /// - Slope close to 0: Indicates a relatively flat or no clear trend in the data.
    /// The magnitude of the slope represents the rate of change in the dependent variable
    /// (y) for each unit change in the independent variable (x).
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);
        _timeBuffer.Add(Input.Time.Ticks, Input.IsNew);

        double slope = 0;

        if (_buffer.Count < 2)
        {
            return slope; // Return 0 when there are fewer than 2 points
        }

        int count = Math.Min(_buffer.Count, _period);
        var values = _buffer.GetSpan().ToArray();

        // Calculate averages
        double sumX = 0, sumY = 0;
        for (int i = 0; i < count; i++)
        {
            sumX += i + 1;
            sumY += values[i];
        }
        double avgX = sumX / count;
        double avgY = sumY / count;

        // Least squares method
        double sumSqX = 0, sumSqY = 0, sumSqXY = 0;
        for (int i = 0; i < count; i++)
        {
            double devX = (i + 1) - avgX;
            double devY = values[i] - avgY;
            sumSqX += devX * devX;
            sumSqY += devY * devY;
            sumSqXY += devX * devY;
        }

        if (sumSqX > 0)
        {
            slope = sumSqXY / sumSqX;
            Intercept = avgY - (slope * avgX);

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
            Line = (slope * count) + Intercept;
        }
        else
        {
            Intercept = null;
            StdDev = null;
            RSquared = null;
            Line = null;
        }

        IsHot = _buffer.Count == _period;
        return slope;
    }
}
