namespace QuanTAlib;

/// <summary>
/// Represents a Huber Loss calculator that combines the best properties of L2 squared loss for normal data
/// and L1 absolute loss for outliers.
/// </summary>
/// <remarks>
/// The Huberloss class calculates the Huber Loss using circular buffers
/// to efficiently manage the actual and predicted data points within the specified period.
/// </remarks>
public class Huberloss : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;
    private readonly double _delta;

    /// <summary>
    /// Initializes a new instance of the Huberloss class with the specified period and delta.
    /// </summary>
    /// <param name="period">The period over which to calculate the Huber Loss.</param>
    /// <param name="delta">The threshold at which to switch from squared to linear loss.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1 or delta is less than or equal to 0.
    /// </exception>
    public Huberloss(int period, double delta = 1.0)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        if (delta <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta must be greater than 0.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        _delta = delta;
        Name = $"Huberloss(period={period}, delta={delta})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mape class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Percentage Error.</param>
    public Huberloss(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Huberloss instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Huberloss instance based on whether new values are being processed.
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
    /// Performs the Huber Loss calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Huber Loss value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Huber Loss using the formula:
    /// L(a, p) = 0.5 * (a - p)^2 for |a - p| <= delta
    /// L(a, p) = delta * |a - p| - 0.5 * delta^2 for |a - p| > delta
    /// where a is the actual value, p is the predicted value, and delta is the threshold.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double huberLoss = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumLoss = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double error = Math.Abs(actualValues[i] - predictedValues[i]);
                if (error <= _delta)
                {
                    sumLoss += 0.5 * error * error;
                }
                else
                {
                    sumLoss += _delta * error - 0.5 * _delta * _delta;
                }
            }

            huberLoss = sumLoss / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return huberLoss;
    }

    /// <summary>
    /// Calculates the Huber Loss for the given actual and predicted values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="predicted">The predicted value.</param>
    /// <returns>The calculated Huber Loss.</returns>
    public double Calc(double actual, double predicted)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, predicted);
        return Calculation();
    }
}
