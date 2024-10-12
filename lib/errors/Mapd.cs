namespace QuanTAlib;

/// <summary>
/// Represents a Mean Absolute Percentage Deviation calculator that measures the average absolute percentage difference
/// between actual values and predicted values.
/// </summary>
/// <remarks>
/// The Mapd class calculates the Mean Absolute Percentage Deviation using circular buffers
/// to efficiently manage the actual and predicted data points within the specified period.
/// </remarks>
public class Mapd : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <summary>
    /// Initializes a new instance of the Mapd class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Deviation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1.
    /// </exception>
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

    /// <summary>
    /// Initializes a new instance of the Mapd class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Deviation.</param>
    public Mapd(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Mapd instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Mapd instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the Mean Absolute Percentage Deviation calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Mean Absolute Percentage Deviation value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Mean Absolute Percentage Deviation using the formula:
    /// MAPD = (sum(|actual - predicted| / |actual|) / n) * 100
    /// where actual is each actual value, predicted is each predicted value, and n is the number of values.
    /// If there's only one value in the buffer or if any actual value is zero, those values are excluded from the calculation.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double mapd = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumOfAbsolutePercentageDeviations = 0;
            int validCount = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                if (actualValues[i] != 0)
                {
                    sumOfAbsolutePercentageDeviations += Math.Abs((actualValues[i] - predictedValues[i]) / actualValues[i]);
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                mapd = (sumOfAbsolutePercentageDeviations / validCount) * 100;
            }
        }

        IsHot = _index >= WarmupPeriod;
        return mapd;
    }

    /// <summary>
    /// Calculates the Mean Absolute Percentage Deviation for the given actual and predicted values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="predicted">The predicted value.</param>
    /// <returns>The calculated Mean Absolute Percentage Deviation.</returns>
    public double Calc(double actual, double predicted)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, predicted);
        return Calculation();
    }
}
