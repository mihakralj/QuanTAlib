using System;
namespace QuanTAlib;

/// <summary>
/// MSE: Mean Squared Error
/// A fundamental error metric that measures the average of squared differences
/// between predicted and actual values. MSE heavily penalizes large errors due
/// to the squaring operation.
/// </summary>
/// <remarks>
/// The MSE calculation process:
/// 1. Calculates error (actual - predicted) for each point
/// 2. Squares each error value
/// 3. Averages the squared errors
///
/// Key characteristics:
/// - Heavily penalizes large errors
/// - Always non-negative
/// - Units are squared (harder to interpret)
/// - More sensitive to outliers than MAE
/// - Differentiable (useful for optimization)
///
/// Formula:
/// MSE = (1/n) * Σ(actual - predicted)²
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mean_squared_error
///     https://www.statisticshowto.com/probability-and-statistics/statistics-definitions/mean-squared-error/
///
/// Note: Often used in optimization due to its mathematical properties
/// </remarks>

public class Mse : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the MSE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Mse(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mse(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MSE.</param>
    public Mse(object source, int period) : this(period)
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

        double mse = 0;
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

            mse = sumSquaredError / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return mse;
    }
}
