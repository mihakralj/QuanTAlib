using System;
namespace QuanTAlib;

/// <summary>
/// Huber Loss: A robust error metric that combines squared error for small deviations
/// and absolute error for large deviations. This provides a balance between the high
/// sensitivity of MSE to outliers and the constant gradient of MAE.
/// </summary>
/// <remarks>
/// The Huber Loss calculation process:
/// 1. For each point, calculates error between actual and predicted values
/// 2. If absolute error ≤ delta: uses squared error (like MSE)
/// 3. If absolute error > delta: uses linear error (like MAE)
/// 4. Averages the losses over the period
///
/// Key characteristics:
/// - Combines benefits of MSE and MAE
/// - Less sensitive to outliers than MSE
/// - More sensitive to small errors than MAE
/// - Differentiable at all points
/// - Adjustable via delta parameter
///
/// Formula:
/// For error e = actual - predicted:
/// L(e) = 0.5 * e² if |e| ≤ δ
/// L(e) = δ * (|e| - 0.5δ) if |e| > δ
///
/// Sources:
///     Peter J. Huber - "Robust Estimation of a Location Parameter"
///     https://projecteuclid.org/euclid.aoms/1177703732
/// </remarks>

public class Huber : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;
    private readonly double _delta;

    /// <param name="period">The number of points over which to calculate the loss.</param>
    /// <param name="delta">The threshold between squared and linear loss (default 1.0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1 or delta is not positive.</exception>
    public Huber(int period, double delta = 1.0)
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

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the loss.</param>
    /// <param name="delta">The threshold between squared and linear loss (default 1.0).</param>
    public Huber(object source, int period, double delta = 1.0) : this(period, delta)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
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

        double huberloss = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumLoss = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double error = actualValues[i] - predictedValues[i];
                double absError = Math.Abs(error);

                if (absError <= _delta)
                {
                    // Squared error for small deviations
                    sumLoss += 0.5 * error * error;
                }
                else
                {
                    // Linear error for large deviations
                    sumLoss += _delta * (absError - 0.5 * _delta);
                }
            }

            huberloss = sumLoss / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return huberloss;
    }
}
