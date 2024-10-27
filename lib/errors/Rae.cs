using System;
namespace QuanTAlib;

/// <summary>
/// RAE: Relative Absolute Error
/// A normalized error metric that compares the total absolute error to the total
/// magnitude of actual values. RAE provides a scale-independent measure of error
/// that is robust to the overall magnitude of the data.
/// </summary>
/// <remarks>
/// The RAE calculation process:
/// 1. Calculates sum of absolute errors
/// 2. Calculates sum of absolute actual values
/// 3. Divides total error by total actual magnitude
///
/// Key characteristics:
/// - Scale-independent (normalized by actual values)
/// - Range typically between 0 and 1
/// - Easy to interpret (0 is perfect, 1 means error equals data magnitude)
/// - Robust to data scale changes
/// - Less sensitive to outliers than squared errors
///
/// Formula:
/// RAE = Σ|actual - predicted| / Σ|actual|
///
/// Sources:
///     https://en.wikipedia.org/wiki/Relative_absolute_error
///     https://www.sciencedirect.com/topics/engineering/relative-absolute-error
///
/// Note: Values greater than 1 indicate predictions worse than using zero
/// </remarks>

public class Rae : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the RAE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Rae(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rae(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the RAE.</param>
    public Rae(object source, int period) : this(period)
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

        double rae = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumAbsoluteError = 0;
            double sumAbsoluteActual = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                sumAbsoluteError += Math.Abs(actualValues[i] - predictedValues[i]);
                sumAbsoluteActual += Math.Abs(actualValues[i]);
            }

            rae = sumAbsoluteError / sumAbsoluteActual;
        }

        IsHot = _index >= WarmupPeriod;
        return rae;
    }
}
