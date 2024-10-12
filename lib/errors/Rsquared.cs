namespace QuanTAlib;

/// <summary>
/// Represents a Coefficient of Determination (R-squared) calculator that measures the proportion of
/// the variance in the dependent variable that is predictable from the independent variable(s).
/// </summary>
/// <remarks>
/// The Rsquared class calculates the Coefficient of Determination using circular buffers
/// to efficiently manage the actual and predicted data points within the specified period.
/// </remarks>
public class Rsquared : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <summary>
    /// Initializes a new instance of the Rsquared class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Coefficient of Determination.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Rsquared(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rsquared(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mape class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Error.</param>
    public Rsquared(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Rsquared instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Rsquared instance based on whether new values are being processed.
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
    /// Performs the Coefficient of Determination calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Coefficient of Determination value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Coefficient of Determination using the formula:
    /// R^2 = 1 - (SSres / SStot)
    /// where SSres is the sum of squared residuals and SStot is the total sum of squares.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double rsquared = 0;
        if (_actualBuffer.Count >= 2)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double actualMean = actualValues.Average();
            double ssRes = 0;
            double ssTot = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double residual = actualValues[i] - predictedValues[i];
                ssRes += residual * residual;

                double deviation = actualValues[i] - actualMean;
                ssTot += deviation * deviation;
            }

            if (ssTot != 0)
            {
                rsquared = 1 - (ssRes / ssTot);
            }
        }

        IsHot = _index >= WarmupPeriod;
        return rsquared;
    }

    /// <summary>
    /// Calculates the Coefficient of Determination for the given actual and predicted values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="predicted">The predicted value.</param>
    /// <returns>The calculated Coefficient of Determination.</returns>
    public double Calc(double actual, double predicted)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, predicted);
        return Calculation();
    }
}
