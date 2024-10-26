using System;

namespace QuanTAlib;

/// <summary>
/// Represents the Mean Absolute Scaled Error (MASE) calculation.
/// </summary>
public class Mase : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;
    private readonly CircularBuffer _naiveBuffer;

    /// <summary>
    /// Initializes a new instance of the Mase class.
    /// </summary>
    /// <param name="period">The period for MASE calculation.</param>
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

    /// <summary>
    /// Initializes a new instance of the Mase class with a source object.
    /// </summary>
    /// <param name="source">The source object for event subscription.</param>
    /// <param name="period">The period for MASE calculation.</param>
    public Mase(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Mase instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
        _naiveBuffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Mase instance.
    /// </summary>
    /// <param name="isNew">Indicates if the input is new.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the MASE calculation.
    /// </summary>
    /// <returns>The calculated MASE value.</returns>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        if (_actualBuffer.Count > 1)
        {
            _naiveBuffer.Add(_actualBuffer.GetSpan()[^2], Input.IsNew);
        }

        double mase = CalculateMase();

        IsHot = _index >= WarmupPeriod;
        return mase;
    }

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

    private static double CalculateSumAbsoluteError(ReadOnlySpan<double> actualValues, ReadOnlySpan<double> predictedValues)
    {
        double sum = 0;
        for (int i = 0; i < actualValues.Length; i++)
        {
            sum += Math.Abs(actualValues[i] - predictedValues[i]);
        }
        return sum;
    }

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
