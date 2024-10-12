namespace QuanTAlib;

/// <summary>
/// Represents a Mean Squared Error calculator that measures the average of the squares
/// of the differences between actual values and predicted values.
/// </summary>
/// <remarks>
/// The Mse class calculates the Mean Squared Error using a circular buffer
/// to efficiently manage the data points within the specified period.
/// </remarks>
public class Mse : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <summary>
    /// Initializes a new instance of the Mse class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Mean Squared Error.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1.
    /// </exception>
    public Mse(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mse(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mape class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Error.</param>
    public Mse(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }
    
    /// <summary>
    /// Initializes the Mse instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
    }


    /// <summary>
    /// Manages the state of the Mse instance based on whether new values are being processed.
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
    /// Performs the Mean Squared Error calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Mean Squared Error value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Mean Squared Error using the formula:
    /// MSE = sum((actual - predicted)^2) / n
    /// where actual is each actual value, predicted is each predicted value, and n is the number of values.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double mse = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumSquaredError = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double error = actualValues[i] - predictedValues[i];
                sumSquaredError += error * error;
            }

            mse = sumSquaredError / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return mse;
    }

    /// <summary>
    /// Calculates the Mean Squared Error for the given actual and predicted values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="predicted">The predicted value.</param>
    /// <returns>The calculated Mean Squared Error.</returns>
    public double Calc(double actual, double predicted)
    {
        Input = new TValue(DateTime.Now, actual);
        _lastValidValue = predicted;
        return Calculation();
    }
}
