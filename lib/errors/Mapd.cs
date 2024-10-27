using System;
namespace QuanTAlib;

/// <summary>
/// MAPD: Mean Absolute Percentage Deviation
/// A percentage-based error metric that measures the average absolute percentage
/// difference between predicted and actual values. MAPD expresses accuracy as a
/// percentage, making it scale-independent and easy to interpret.
/// </summary>
/// <remarks>
/// The MAPD calculation process:
/// 1. Calculates absolute percentage difference for each point
/// 2. Sums all absolute percentage differences
/// 3. Divides by the number of observations
///
/// Key characteristics:
/// - Scale-independent (percentage-based)
/// - Easy to interpret (0-100% range)
/// - Useful for comparing different scales
/// - Cannot handle zero actual values
/// - Asymmetric (treats over/under predictions differently)
///
/// Formula:
/// MAPD = (1/n) * Σ|((actual - predicted) / actual)|
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mean_absolute_percentage_error
///     https://www.statisticshowto.com/mean-absolute-percentage-error-mape/
///
/// Note: Also known as MAPE (Mean Absolute Percentage Error) in some contexts
/// </remarks>

public class Mapd : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the MAPD.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Mapd(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mapd(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MAPD.</param>
    public Mapd(object source, int period) : this(period)
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

        double mapd = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumAbsolutePercentageDeviation = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                if (actualValues[i] != 0)
                {
                    sumAbsolutePercentageDeviation += Math.Abs((actualValues[i] - predictedValues[i]) / actualValues[i]);
                }
            }

            mapd = sumAbsolutePercentageDeviation / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return mapd;
    }
}
