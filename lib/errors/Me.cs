using System;
namespace QuanTAlib;

/// <summary>
/// ME: Mean Error
/// A basic error metric that measures the average difference between actual and
/// predicted values. Unlike MAE, it allows positive and negative errors to cancel
/// out, making it useful for detecting systematic bias in predictions.
/// </summary>
/// <remarks>
/// The ME calculation process:
/// 1. Calculates error (actual - predicted) for each point
/// 2. Sums all errors (allowing cancellation)
/// 3. Divides by the number of observations
///
/// Key characteristics:
/// - Same units as input data
/// - Can detect systematic bias
/// - Positive ME indicates underprediction
/// - Negative ME indicates overprediction
/// - Errors can cancel out
///
/// Formula:
/// ME = (1/n) * Σ(actual - predicted)
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mean_signed_difference
///     https://www.statisticshowto.com/mean-error/
///
/// Note: Also known as Mean Bias Error (MBE) or Mean Signed Difference (MSD)
/// </remarks>

public class Me : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the ME.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Me(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Me(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the ME.</param>
    public Me(object source, int period) : this(period)
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

        double me = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumError = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                sumError += actualValues[i] - predictedValues[i];
            }

            me = sumError / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return me;
    }
}
