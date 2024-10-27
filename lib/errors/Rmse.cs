using System;
namespace QuanTAlib;

/// <summary>
/// RMSE: Root Mean Square Error
/// A widely used error metric that measures the square root of the average squared
/// differences between predicted and actual values. RMSE provides error measurements
/// in the same units as the original data.
/// </summary>
/// <remarks>
/// The RMSE calculation process:
/// 1. Calculates error (actual - predicted) for each point
/// 2. Squares each error value
/// 3. Averages the squared errors
/// 4. Takes the square root of the average
///
/// Key characteristics:
/// - Same units as input data (unlike MSE)
/// - Penalizes large errors more than small ones
/// - Always non-negative
/// - More interpretable than MSE
/// - Commonly used in regression problems
///
/// Formula:
/// RMSE = √((1/n) * Σ(actual - predicted)²)
///
/// Sources:
///     https://en.wikipedia.org/wiki/Root-mean-square_deviation
///     https://www.statisticshowto.com/probability-and-statistics/regression-analysis/rmse-root-mean-square-error/
///
/// Note: Square root of MSE, making it more interpretable in original units
/// </remarks>

public class Rmse : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the RMSE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Rmse(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rmse(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the RMSE.</param>
    public Rmse(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
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

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        // If no predicted value provided, use mean of actual values
        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double rmse = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumSquaredError = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double error = actualValues[i] - predictedValues[i];
                sumSquaredError += error * error;
            }

            rmse = Math.Sqrt(sumSquaredError / _actualBuffer.Count);
        }

        IsHot = _index >= WarmupPeriod;
        return rmse;
    }
}
