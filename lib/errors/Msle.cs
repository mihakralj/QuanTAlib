using System;
namespace QuanTAlib;

/// <summary>
/// MSLE: Mean Squared Logarithmic Error
/// A variation of MSE that operates on log-transformed values. MSLE is particularly
/// useful for data with exponential growth or when errors in larger values should
/// not be penalized more heavily than errors in smaller values.
/// </summary>
/// <remarks>
/// The MSLE calculation process:
/// 1. Adds 1 to both actual and predicted values (to handle zeros)
/// 2. Takes natural log of both values
/// 3. Calculates squared difference of logs
/// 4. Averages the squared differences
///
/// Key characteristics:
/// - Scale-independent due to log transformation
/// - Penalizes underestimates more than overestimates
/// - Handles exponential trends well
/// - More sensitive to relative differences
/// - Can handle zero values (adds 1 before log)
///
/// Formula:
/// MSLE = (1/n) * Σ(log(actual + 1) - log(predicted + 1))²
///
/// Sources:
///     https://scikit-learn.org/stable/modules/model_evaluation.html#mean-squared-logarithmic-error
///     https://medium.com/analytics-vidhya/root-mean-square-log-error-rmse-vs-rmlse-935c6cc1802a
///
/// Note: Often used in cases where target values follow exponential growth
/// </remarks>

public class Msle : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the MSLE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Msle(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Msle(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MSLE.</param>
    public Msle(object source, int period) : this(period)
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

        double msle = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumSquaredLogError = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double logActual = Math.Log(actualValues[i] + 1);
                double logPredicted = Math.Log(predictedValues[i] + 1);
                double error = logActual - logPredicted;
                sumSquaredLogError += error * error;
            }

            msle = sumSquaredLogError / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return msle;
    }
}
