using System;

namespace QuanTAlib;

/// <summary>
/// Represents a Relative Strength Index (RSI) calculator following Wilder's algorithm.
/// </summary>
public class Rsi : AbstractBase
{
    private readonly Rma _avgGain;
    private readonly Rma _avgLoss;
    private double _prevValue, _p_prevValue;

    public Rsi(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _avgGain = new(period, useSma: true);
        _avgLoss = new(period, useSma: true);
        _index = 0;
        WarmupPeriod = period + 1;
        Name = $"RSI({period})";
    }

    /// <summary>
    /// Initializes a new instance of the RSI class with a data source.
    /// </summary>
    /// <param name="source">The source object that publishes data.</param>
    /// <param name="period">The number of data points to consider.</param>
    public Rsi(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevValue = _prevValue;
        }
        else
        {
            _prevValue = _p_prevValue;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 1)
        {
            _prevValue = Input.Value;
        }

        double change = Input.Value - _prevValue;
        double gain = Math.Max(change, 0);
        double loss = Math.Max(-change, 0);
        _prevValue = Input.Value;

        _avgGain.Calc(gain, IsNew: Input.IsNew);
        _avgLoss.Calc(loss, IsNew: Input.IsNew);

        double rsi = (_avgLoss.Value > 0) ? 100 - (100 / (1 + (_avgGain.Value / _avgLoss.Value))) : 100;

        return rsi;
    }
}
