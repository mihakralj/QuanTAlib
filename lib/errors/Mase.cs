namespace QuanTAlib;

/// <summary>
/// Represents a Mean Absolute Scaled Error calculator that measures the ratio of the mean absolute error
/// of the forecast values to the mean absolute error of the naive forecast.
/// </summary>
/// <remarks>
/// The Mase class calculates the Mean Absolute Scaled Error using circular buffers
/// to efficiently manage the data points within the specified period.
/// </remarks>
public class Mase : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _forecastBuffer;
    private readonly int _period;

    /// <summary>
    /// Initializes a new instance of the Mase class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Mean Absolute Scaled Error.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 3.
    /// </exception>
    public Mase(int period)
    {
        if (period < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 3.");
        }
        _period = period;
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _forecastBuffer = new CircularBuffer(period);
        Name = $"Mase(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mase class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Mean Absolute Scaled Error.</param>
    public Mase(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Mase instance by clearing the buffers.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _forecastBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Mase instance based on whether new values are being processed.
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
    /// Performs the Mean Absolute Scaled Error calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Mean Absolute Scaled Error value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Mean Absolute Scaled Error using the formula:
    /// MASE = mean(|actual - forecast|) / mean(|actual[t] - actual[t-1]|)
    /// where actual is each actual value and forecast is each forecast value.
    /// If there are fewer than 3 values in the buffers, the method returns 0.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double forecast = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _forecastBuffer.Add(forecast, Input.IsNew);

        double mase = 0;
        if (_actualBuffer.Count >= 3)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var forecastValues = _forecastBuffer.GetSpan().ToArray();

            double sumAbsoluteError = 0;
            double sumAbsoluteNaiveError = 0;

            int count = Math.Min(_actualBuffer.Count, _period);

            for (int i = 1; i < count; i++)
            {
                sumAbsoluteError += Math.Abs(actualValues[i] - forecastValues[i]);
                sumAbsoluteNaiveError += Math.Abs(actualValues[i] - actualValues[i - 1]);
            }

            double meanAbsoluteError = sumAbsoluteError / (count - 1);
            double meanAbsoluteNaiveError = sumAbsoluteNaiveError / (count - 1);

            if (meanAbsoluteNaiveError != 0)
            {
                mase = meanAbsoluteError / meanAbsoluteNaiveError;
            }
        }

        IsHot = _index >= WarmupPeriod;
        return mase;
    }

    /// <summary>
    /// Calculates the Mean Absolute Scaled Error for the given actual and forecast values.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="forecast">The forecast value.</param>
    /// <returns>The calculated Mean Absolute Scaled Error.</returns>
    public double Calc(double actual, double forecast)
    {
        Input = new TValue(DateTime.Now, actual);
        Input2 = new TValue(DateTime.Now, forecast);
        return Calculation();
    }
}
