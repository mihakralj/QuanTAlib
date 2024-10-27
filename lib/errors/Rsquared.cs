using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// R-squared: Coefficient of Determination
/// A statistical measure that represents the proportion of variance in the dependent
/// variable that is predictable from the independent variable. R-squared provides
/// a measure of how well the predictions approximate the actual data.
/// </summary>
/// <remarks>
/// The R-squared calculation process:
/// 1. Calculates total sum of squares (variance from mean)
/// 2. Calculates residual sum of squares (prediction errors)
/// 3. Computes 1 - (residual SS / total SS)
///
/// Key characteristics:
/// - Range is typically 0 to 1
/// - 1 indicates perfect prediction
/// - 0 indicates prediction no better than mean
/// - Scale-independent
/// - Widely used in regression analysis
///
/// Formula:
/// R² = 1 - (Σ(actual - predicted)² / Σ(actual - mean(actual))²)
///
/// Sources:
///     https://en.wikipedia.org/wiki/Coefficient_of_determination
///     https://www.statisticshowto.com/probability-and-statistics/coefficient-of-determination-r-squared/
///
/// Note: Can be negative if predictions are worse than using the mean
/// </remarks>

public class Rsquared : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the R-squared value.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Rsquared(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rsquared(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the R-squared value.</param>
    public Rsquared(object source, int period) : this(period)
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

        double rsquared = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double meanActual = actualValues.Average();
            double sumSquaredTotal = 0;
            double sumSquaredResidual = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double deviation = actualValues[i] - meanActual;
                sumSquaredTotal += deviation * deviation;
                double error = actualValues[i] - predictedValues[i];
                sumSquaredResidual += error * error;
            }

            if (sumSquaredTotal != 0)
            {
                rsquared = 1 - (sumSquaredResidual / sumSquaredTotal);
            }
        }

        IsHot = _index >= WarmupPeriod;
        return rsquared;
    }
}
