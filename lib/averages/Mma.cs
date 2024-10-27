using System;
namespace QuanTAlib;

/// <summary>
/// MMA: Modified Moving Average
/// A moving average that combines a simple moving average with a weighted component
/// to provide a balanced smoothing effect. The weighting scheme emphasizes central
/// values while maintaining overall data representation.
/// </summary>
/// <remarks>
/// The MMA calculation process:
/// 1. Calculates the simple moving average component (T/period)
/// 2. Calculates a weighted sum with symmetric weights around the center
/// 3. Combines both components using the formula: SMA + 6*WeightedSum/((period+1)*period)
///
/// Key characteristics:
/// - Combines simple and weighted moving averages
/// - Symmetric weighting around the center
/// - Better balance between smoothing and responsiveness
/// - Reduces lag compared to simple moving average
/// - Maintains stability through dual-component approach
///
/// Implementation:
///     Based on modified moving average principles combining
///     simple and weighted components for optimal smoothing
/// </remarks>

public class Mma : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _buffer;
    private double _lastMma;

    /// <param name="period">The number of periods used in the MMA calculation. Must be at least 2.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    public Mma(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        _period = period;
        _buffer = new CircularBuffer(period);
        Name = "Mma";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the MMA calculation.</param>
    public Mma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _lastMma = 0;
        _buffer.Clear();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        if (_index >= _period)
        {
            double T = _buffer.Sum();
            double S = CalculateWeightedSum();
            _lastMma = (T / _period) + (6 * S) / ((_period + 1) * _period);
        }
        else
        {
            // Use simple average until we have enough data points
            _lastMma = _buffer.Average();
        }

        IsHot = _index >= _period;
        return _lastMma;
    }

    /// <summary>
    /// Calculates the weighted sum component of the MMA.
    /// The weights are symmetric around the center, decreasing linearly from the center outward.
    /// </summary>
    /// <returns>The weighted sum of the data points.</returns>
    private double CalculateWeightedSum()
    {
        double sum = 0;
        for (int i = 0; i < _period; i++)
        {
            double weight = (_period - (2 * i + 1)) / 2.0;
            sum += weight * _buffer[^(i + 1)];
        }
        return sum;
    }
}
