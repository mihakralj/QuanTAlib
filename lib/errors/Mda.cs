namespace QuanTAlib;

/// <summary>
/// Represents a Mean Directional Accuracy calculator that measures the average accuracy
/// of predicted directional changes compared to actual directional changes.
/// </summary>
/// <remarks>
/// The Mda class calculates the Mean Directional Accuracy using a circular buffer
/// to efficiently manage the data points within the specified period.
/// Mean Directional Accuracy is useful in financial analysis for evaluating the performance
/// of forecasting models in predicting the direction of price movements.
/// </remarks>
public class Mda : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _forecastBuffer;

    /// <summary>
    /// Initializes a new instance of the Mda class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Mean Directional Accuracy.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Mda(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        WarmupPeriod = 1;
        _actualBuffer = new CircularBuffer(period);
        _forecastBuffer = new CircularBuffer(period);
        Name = $"Mda(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mda class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Directional Accuracy.</param>
    public Mda(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Mda instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _forecastBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Mda instance based on whether new values are being processed.
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
    /// Performs the Mean Directional Accuracy calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Mean Directional Accuracy value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Mean Directional Accuracy using the formula:
    /// MDA = (number of correct directional predictions / total number of predictions) * 100
    /// A correct directional prediction is when the sign of the actual change matches
    /// the sign of the predicted change.
    /// The result is expressed as a percentage, where 100% indicates perfect directional accuracy
    /// and 50% indicates performance no better than random guessing.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double forecast = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _forecastBuffer.Add(forecast, Input.IsNew);

        double mda = 0;
        if (_actualBuffer.Count > 1)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var forecastValues = _forecastBuffer.GetSpan().ToArray();

            int correctPredictions = 0;
            int totalPredictions = actualValues.Length - 1;

            for (int i = 1; i < actualValues.Length; i++)
            {
                double actualChange = actualValues[i] - actualValues[i - 1];
                double forecastChange = forecastValues[i] - actualValues[i - 1];

                if ((actualChange >= 0 && forecastChange >= 0) || (actualChange < 0 && forecastChange < 0))
                {
                    correctPredictions++;
                }
            }

            mda = (double)correctPredictions / totalPredictions * 100;
        }

        IsHot = _actualBuffer.Count > 1; // MDA calc is valid from bar 2
        return mda;
    }

    /// <summary>
    /// Calculates the Mean Directional Accuracy for the given actual and forecast values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="forecast">The forecast value.</param>
    /// <returns>The calculated Mean Directional Accuracy.</returns>
    public double Calc(double actual, double forecast)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, forecast);
        return Calculation();
    }
}
