namespace QuanTAlib;

/// <summary>
/// Represents a Relative Absolute Error calculator that measures the ratio of the sum of absolute errors
/// to the sum of absolute differences between actual values and the mean of actual values.
/// </summary>
/// <remarks>
/// The Rae class calculates the Relative Absolute Error using circular buffers
/// to efficiently manage the data points within the specified period.
/// </remarks>
public class Rae : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <summary>
    /// Initializes a new instance of the Rae class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Relative Absolute Error.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Rae(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rae(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mape class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Error.</param>
    public Rae(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Rae instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Rae instance based on whether new values are being processed.
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
    /// Performs the Relative Absolute Error calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Relative Absolute Error value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Relative Absolute Error using the formula:
    /// RAE = sum(|actual - predicted|) / sum(|actual - mean(actual)|)
    /// where actual is each actual value, predicted is each predicted value, and mean(actual) is the average of actual values.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double rae = 0;
        if (_actualBuffer.Count >= 2)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double actualMean = actualValues.Average();
            double sumAbsoluteError = 0;
            double sumAbsoluteDifferenceFromMean = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                sumAbsoluteError += Math.Abs(actualValues[i] - predictedValues[i]);
                sumAbsoluteDifferenceFromMean += Math.Abs(actualValues[i] - actualMean);
            }

            if (sumAbsoluteDifferenceFromMean != 0)
            {
                rae = sumAbsoluteError / sumAbsoluteDifferenceFromMean;
            }
        }

        IsHot = _index >= WarmupPeriod;
        return rae;
    }

    /// <summary>
    /// Calculates the Relative Absolute Error for the given actual and predicted values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="predicted">The predicted value.</param>
    /// <returns>The calculated Relative Absolute Error.</returns>
    public double Calc(double actual, double predicted)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, predicted);
        return Calculation();
    }
}
