using System;
namespace QuanTAlib;

/// <summary>
/// SMAPE: Symmetric Mean Absolute Percentage Error
/// A variation of MAPE that treats positive and negative errors symmetrically.
/// SMAPE uses the average of actual and predicted values in the denominator,
/// making it more robust than MAPE for values close to zero.
/// </summary>
/// <remarks>
/// The SMAPE calculation process:
/// 1. Calculates absolute difference between actual and predicted
/// 2. Divides by sum of absolute actual and predicted values
/// 3. Averages these ratios and multiplies by 200%
///
/// Key characteristics:
/// - Symmetric treatment of errors
/// - Range is 0% to 200%
/// - More robust than MAPE near zero
/// - Scale-independent
/// - Handles both positive and negative values
///
/// Formula:
/// SMAPE = (200/n) * Σ|actual - predicted| / (|actual| + |predicted|)
///
/// Sources:
///     https://en.wikipedia.org/wiki/Symmetric_mean_absolute_percentage_error
///     https://www.sciencedirect.com/science/article/abs/pii/0169207085900059
///
/// Note: More stable than MAPE when actual values are close to zero
/// </remarks>

public class Smape : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the SMAPE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Smape(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Smape(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the SMAPE.</param>
    public Smape(object source, int period) : this(period)
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

        double smape = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumSymmetricAbsolutePercentageError = 0;
            int validCount = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double denominator = Math.Abs(actualValues[i]) + Math.Abs(predictedValues[i]);
                if (denominator != 0)
                {
                    sumSymmetricAbsolutePercentageError += Math.Abs(actualValues[i] - predictedValues[i]) / denominator;
                    validCount++;
                }
            }

            smape = validCount > 0 ? (200 * sumSymmetricAbsolutePercentageError / validCount) : 0;
        }

        IsHot = _index >= WarmupPeriod;
        return smape;
    }
}
