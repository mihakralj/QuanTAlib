namespace QuanTAlib;

/// <summary>
/// Represents a Symmetric Mean Absolute Percentage Error calculator that measures the percentage difference
/// between actual and predicted values, using a symmetric formula to handle both positive and negative errors equally.
/// </summary>
/// <remarks>
/// The Smape class calculates the Symmetric Mean Absolute Percentage Error using circular buffers
/// to efficiently manage the data points within the specified period.
/// </remarks>
public class Smape : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <summary>
    /// Initializes a new instance of the Smape class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Symmetric Mean Absolute Percentage Error.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1.
    /// </exception>
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

    /// <summary>
    /// Initializes a new instance of the Mape class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Error.</param>
    public Smape(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Smape instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Smape instance based on whether new values are being processed.
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
    /// Performs the Symmetric Mean Absolute Percentage Error calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Symmetric Mean Absolute Percentage Error value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Symmetric Mean Absolute Percentage Error using the formula:
    /// SMAPE = (100% / n) * sum(2 * |actual - predicted| / (|actual| + |predicted|))
    /// where actual is each actual value, predicted is each predicted value, and n is the number of values.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double smape = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumSymmetricPercentageError = 0;
            int validCount = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double denominator = Math.Abs(actualValues[i]) + Math.Abs(predictedValues[i]);
                if (denominator != 0)
                {
                    sumSymmetricPercentageError += 2 * Math.Abs(actualValues[i] - predictedValues[i]) / denominator;
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                smape = (100.0 / validCount) * sumSymmetricPercentageError;
            }
        }

        IsHot = _index >= WarmupPeriod;
        return smape;
    }

    /// <summary>
    /// Calculates the Symmetric Mean Absolute Percentage Error for the given actual and predicted values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="predicted">The predicted value.</param>
    /// <returns>The calculated Symmetric Mean Absolute Percentage Error.</returns>
    public double Calc(double actual, double predicted)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, predicted);
        return Calculation();
    }
}
