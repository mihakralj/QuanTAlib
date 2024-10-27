using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// SLOPE: Linear Regression Trend Measure
/// A statistical measure that calculates the rate of change using linear regression.
/// Slope indicates the direction and steepness of a trend, providing insights into
/// momentum and potential trend changes.
/// </summary>
/// <remarks>
/// The Slope calculation process:
/// 1. Calculates means of x and y values
/// 2. Computes deviations from means
/// 3. Applies least squares method
/// 4. Provides additional regression statistics
///
/// Key characteristics:
/// - Measures trend direction and strength
/// - Provides rate of change
/// - Scale-dependent measure
/// - Includes regression statistics
/// - Time-weighted calculation
///
/// Formula:
/// slope = Σ((x - x̄)(y - ȳ)) / Σ((x - x̄)²)
/// where:
/// x = time points
/// y = price values
/// x̄, ȳ = respective means
///
/// Market Applications:
/// - Trend identification
/// - Momentum measurement
/// - Support/resistance angles
/// - Price target projection
/// - Trend strength analysis
///
/// Sources:
///     https://en.wikipedia.org/wiki/Simple_linear_regression
///     "Technical Analysis of Financial Markets" - John J. Murphy
///
/// Note: Provides additional regression statistics (R², intercept)
/// </remarks>

public class Slope : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _buffer;
    private readonly CircularBuffer _timeBuffer;

    /// <summary>Gets the y-intercept of the regression line.</summary>
    public double? Intercept { get; private set; }

    /// <summary>Gets the standard deviation of the y-values.</summary>
    public double? StdDev { get; private set; }

    /// <summary>Gets the R-squared value, indicating regression fit quality.</summary>
    public double? RSquared { get; private set; }

    /// <summary>Gets the last point on the regression line.</summary>
    public double? Line { get; private set; }

    /// <param name="period">The number of points to consider for slope calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than or equal to 1.</exception>
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

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for slope calculation.</param>
    public Slope(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

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
        _timeBuffer.Add(Input.Time.Ticks, Input.IsNew);

        double slope = 0;
        if (_buffer.Count < 2)
        {
            return slope; // Need at least 2 points
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

        // Least squares regression
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
            // Calculate slope and related statistics
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

            // Calculate regression line endpoint
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
