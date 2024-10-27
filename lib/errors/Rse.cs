using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// RSE: Relative Squared Error
/// A normalized error metric that compares the squared error of predictions to
/// the variance of actual values. RSE provides a scale-independent measure of
/// prediction accuracy relative to the inherent variability in the data.
/// </summary>
/// <remarks>
/// The RSE calculation process:
/// 1. Calculates sum of squared prediction errors
/// 2. Calculates sum of squared deviations from mean (variance)
/// 3. Divides squared error by variance and takes square root
///
/// Key characteristics:
/// - Scale-independent (normalized by data variance)
/// - Range typically between 0 and 1
/// - Easy interpretation relative to data variance
/// - Penalizes large errors more than small ones
/// - Accounts for data variability
///
/// Formula:
/// RSE = √(Σ(actual - predicted)² / Σ(actual - mean(actual))²)
///
/// Sources:
///     https://en.wikipedia.org/wiki/Relative_squared_error
///     https://www.sciencedirect.com/topics/engineering/relative-squared-error
///
/// Note: Values less than 1 indicate predictions better than using mean
/// </remarks>

public class Rse : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the RSE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Rse(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rse(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the RSE.</param>
    public Rse(object source, int period) : this(period)
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

        double rse = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumSquaredError = 0;
            double sumSquaredActual = 0;
            double meanActual = actualValues.Average();

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double error = actualValues[i] - predictedValues[i];
                sumSquaredError += error * error;
                double deviation = actualValues[i] - meanActual;
                sumSquaredActual += deviation * deviation;
            }

            rse = Math.Sqrt(sumSquaredError / sumSquaredActual);
        }

        IsHot = _index >= WarmupPeriod;
        return rse;
    }
}
