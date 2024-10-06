using System;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

public class Zlema : AbstractBase
{
    private readonly int _period;
    private CircularBuffer? _buffer;
    private double _alpha;
    private int _lag;
    private double _lastZLEMA, _p_lastZLEMA;

    public Zlema(int period) : base()
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        WarmupPeriod = period;
        _alpha = 2.0 / (_period + 1);
        _lag = (_period - 1) / 2;
        Name = $"Zlema({_period})";
        Init();
    }

    public Zlema(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer = new CircularBuffer(_period);
        _lastZLEMA = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastZLEMA = _lastZLEMA;
        }
        else
        {
            _lastZLEMA = _p_lastZLEMA;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer!.Add(Input.Value, Input.IsNew);

        int lag = Math.Max(Math.Min((int)((_period - 1) * 0.5), _buffer.Count - 1), 0) + 1;
        double zlValue = 2 * Input.Value - _buffer[_buffer.Count - lag];

        // Dynamic alpha factor for index <= period
        double k = (_index <= _period) ? (2.0 / (_index + 1)) : _alpha;
        double zlema = (zlValue - _lastZLEMA) * k + _lastZLEMA;

        _lastZLEMA = zlema;
        IsHot = _index >= WarmupPeriod;

        return zlema;
    }
}