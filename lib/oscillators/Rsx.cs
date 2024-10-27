using System;

namespace QuanTAlib;

/// <summary>
/// Jurik's superior replacement for RSI
/// </summary>
public class Rsx : AbstractBase
{
    private readonly Rma _avgGain;
    private readonly Rma _avgLoss;
    private readonly Jma _rsx;
    private double _prevValue, _p_prevValue;

    public Rsx(int period = 14, int phase = 0, double factor = 0.55)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _avgGain = new(period);
        _avgLoss = new(period);
        _rsx = new(8, 100, 0.25, 3);
        _index = 0;
        WarmupPeriod = period + 1;
        Name = $"RSX({period})";
    }

    /// <summary>
    /// Initializes a new instance of the RSX class with a data source.
    /// </summary>
    /// <param name="source">The source object that publishes data.</param>
    /// <param name="period">The number of data points to consider.</param>
    /// <param name="phase">The phase parameter.</param>
    /// <param name="factor">The factor parameter.</param>
    public Rsx(object source, int period, int phase = 0, double factor = 0.55) : this(period, phase, factor)
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
        double rsx = _rsx.Calc(rsi, Input.IsNew);

        return rsx;
    }
}
