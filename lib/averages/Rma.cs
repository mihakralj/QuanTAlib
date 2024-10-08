using System;

namespace QuanTAlib;
/// <summary>
/// RMA: Relative Moving Average (also known as Wilder's Moving Average)
/// RMA is similar to EMA but uses a different smoothing factor.
/// </summary>
/// <remarks>
/// Key characteristics:
/// - Uses no buffer, relying only on the previous RMA value.
/// - The weight of new data points (alpha) is calculated as 1 / period.
/// - Provides a smoother curve compared to SMA and EMA, reacting more slowly to price changes.
///
/// Calculation method:
/// RMA = (Previous RMA * (period - 1) + New Data) / period
///
/// Sources:
/// - https://www.tradingview.com/pine-script-reference/v5/#fun_ta{dot}rma
/// - https://www.investopedia.com/terms/w/wilders-smoothing.asp
/// </remarks>
public class Rma : AbstractBase
{
    private readonly int _period;
    private double _lastRma;
    private readonly double _alpha;
    private double _savedLastRma;

    public Rma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        WarmupPeriod = period * 2;
        _alpha = 1.0 / _period;  // Wilder's smoothing factor
        Name = $"Rma({_period})";
        Init();
    }

    public Rma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _lastRma = 0;
        _savedLastRma = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _savedLastRma = _lastRma;
            _lastValidValue = Input.Value;
            _index++;
        }
        else
        {
            _lastRma = _savedLastRma;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double rma;

        if (_index == 1)
        {
            rma = Input.Value;
        }
        else if (_index <= _period)
        {
            // Simple average during initial period
            rma = (_lastRma * (_index - 1) + Input.Value) / _index;
        }
        else
        {
            // Wilder's smoothing method
            rma = _alpha * (_lastRma -  Input.Value) + _lastRma;
        }

        _lastRma = rma;
        IsHot = _index >= WarmupPeriod;

        return rma;
    }
}
