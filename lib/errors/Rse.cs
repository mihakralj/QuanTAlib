namespace QuanTAlib;

/// <summary>
/// Represents a Relative Squared Error calculator that measures the ratio of the sum of squared errors
/// to the sum of squared differences between actual values and the mean of actual values.
/// </summary>
/// <remarks>
/// The Rse class calculates the Relative Squared Error using circular buffers
/// to efficiently manage the data points within the specified period.
/// </remarks>
public class Rse : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <summary>
    /// Initializes a new instance of the Rse class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Relative Squared Error.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Rse(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rse(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mape class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Error.</param>
    public Rse(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Rse instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Rse instance based on whether new values are being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current inputs are new values.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the Relative Squared Error calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Relative Squared Error value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Relative Squared Error using the formula:
    /// RSE = sum((actual - predicted)^2) / sum((actual - mean(actual))^2)
    /// where actual is each actual value, predicted is each predicted value, and mean(actual) is the average of actual values.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double rse = 0;
        if (_actualBuffer.Count >= 2)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double actualMean = actualValues.Average();
            double sumSquaredError = 0;
            double sumSquaredDifferenceFromMean = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double error = actualValues[i] - predictedValues[i];
                sumSquaredError += error * error;

                double differenceFromMean = actualValues[i] - actualMean;
                sumSquaredDifferenceFromMean += differenceFromMean * differenceFromMean;
            }

            if (sumSquaredDifferenceFromMean != 0)
            {
                rse = sumSquaredError / sumSquaredDifferenceFromMean;
            }
        }

        IsHot = _index >= WarmupPeriod;
        return rse;
    }

    /// <summary>
    /// Calculates the Relative Squared Error for the given actual and predicted values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="predicted">The predicted value.</param>
    /// <returns>The calculated Relative Squared Error.</returns>
    public double Calc(double actual, double predicted)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, predicted);
        return Calculation();
    }
}
