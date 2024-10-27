using System;
namespace QuanTAlib;

/// <summary>
/// MDA: Mean Directional Accuracy
/// A metric that measures how well a forecast predicts the direction of change
/// rather than the magnitude. MDA focuses on whether the predicted movement
/// (up or down) matches the actual movement.
/// </summary>
/// <remarks>
/// The MDA calculation process:
/// 1. For each consecutive pair of points:
///    - Calculate direction of actual change
///    - Calculate direction of predicted change
///    - Compare directions (match = 1, mismatch = 0)
/// 2. Average the directional matches
///
/// Key characteristics:
/// - Scale-independent (only considers direction)
/// - Range is 0 to 1 (easy interpretation)
/// - Useful for trend prediction evaluation
/// - Ignores magnitude of changes
/// - Equal weight to all directional changes
///
/// Formula:
/// MDA = (1/(n-1)) * Σ(sign(actual[t] - actual[t-1]) == sign(pred[t] - pred[t-1]))
///
/// Sources:
///     https://www.sciencedirect.com/science/article/abs/pii/S0169207016000121
///     "Evaluating Forecasting Performance" - International Journal of Forecasting
/// </remarks>

public class Mda : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the MDA.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Mda(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mda(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MDA.</param>
    public Mda(object source, int period) : this(period)
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

        double mda = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumDirectionalAccuracy = 0;
            for (int i = 1; i < _actualBuffer.Count; i++)
            {
                double actualDirection = Math.Sign(actualValues[i] - actualValues[i - 1]);
                double predictedDirection = Math.Sign(predictedValues[i] - predictedValues[i - 1]);
                sumDirectionalAccuracy += (actualDirection == predictedDirection) ? 1 : 0;
            }

            mda = sumDirectionalAccuracy / (_actualBuffer.Count - 1);
        }

        IsHot = _index >= WarmupPeriod;
        return mda;
    }
}
