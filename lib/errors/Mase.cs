using System;
namespace QuanTAlib;

/// <summary>
/// MASE: Mean Absolute Scaled Error
/// A scale-free error metric that compares the mean absolute error of the forecast
/// with the mean absolute error of the naive forecast. MASE is particularly useful
/// for comparing forecast accuracy across different datasets.
/// </summary>
/// <remarks>
/// The MASE calculation process:
/// 1. Calculates mean absolute error of the forecast
/// 2. Calculates mean absolute error of naive forecast (using previous value)
/// 3. Divides forecast error by naive forecast error
///
/// Key characteristics:
/// - Scale-free (independent of data scale)
/// - Handles zero values unlike percentage errors
/// - Symmetric (treats over/under predictions equally)
/// - Easy interpretation (MASE < 1 means better than naive forecast)
/// - Robust to outliers
///
/// Formula:
/// MASE = MAE(forecast) / MAE(naive_forecast)
/// where naive_forecast[t] = actual[t-1]
///
/// Sources:
///     Rob J. Hyndman - "Another Look at Forecast-Accuracy Metrics for Intermittent Demand"
///     https://robjhyndman.com/papers/another-look-at-measures-of-forecast-accuracy/
/// </remarks>

public class Mase : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;
    private readonly CircularBuffer _naiveBuffer;

    /// <param name="period">The number of points over which to calculate the MASE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Mase(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        _naiveBuffer = new CircularBuffer(period);
        Name = $"Mase(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MASE.</param>
    public Mase(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
        _naiveBuffer.Clear();
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

        // Naive forecast uses previous actual value
        if (_actualBuffer.Count > 1)
        {
            _naiveBuffer.Add(_actualBuffer.GetSpan()[^2], Input.IsNew);
        }

        double mase = CalculateMase();

        IsHot = _index >= WarmupPeriod;
        return mase;
    }

    /// <summary>
    /// Calculates the MASE value by comparing forecast error to naive forecast error.
    /// </summary>
    /// <returns>The calculated MASE value, or positive infinity if naive error is zero.</returns>
    private double CalculateMase()
    {
        if (_actualBuffer.Count <= 1) return 0;

        ReadOnlySpan<double> actualValues = _actualBuffer.GetSpan();
        ReadOnlySpan<double> predictedValues = _predictedBuffer.GetSpan();
        ReadOnlySpan<double> naiveValues = _naiveBuffer.GetSpan();

        double sumAbsoluteError = CalculateSumAbsoluteError(actualValues, predictedValues);
        double _naiveForecastError = CalculateNaiveForecastError(actualValues, naiveValues);

        return _naiveForecastError != 0 ? (sumAbsoluteError / _actualBuffer.Count) / _naiveForecastError : double.PositiveInfinity;
    }

    /// <summary>
    /// Calculates the sum of absolute errors between actual and predicted values.
    /// </summary>
    private static double CalculateSumAbsoluteError(ReadOnlySpan<double> actualValues, ReadOnlySpan<double> predictedValues)
    {
        double sum = 0;
        for (int i = 0; i < actualValues.Length; i++)
        {
            sum += Math.Abs(actualValues[i] - predictedValues[i]);
        }
        return sum;
    }

    /// <summary>
    /// Calculates the naive forecast error using the previous value as prediction.
    /// </summary>
    private static double CalculateNaiveForecastError(ReadOnlySpan<double> actualValues, ReadOnlySpan<double> naiveValues)
    {
        double sum = 0;
        for (int i = 1; i < actualValues.Length; i++)
        {
            sum += Math.Abs(actualValues[i] - naiveValues[i - 1]);
        }
        return sum / (actualValues.Length - 1);
    }
}
