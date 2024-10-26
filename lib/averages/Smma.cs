using System;

namespace QuanTAlib;

public class Smma : AbstractBase
{
    private readonly int _period;
    private CircularBuffer? _buffer;
    private double _lastSmma, _p_lastSmma;

    public Smma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        WarmupPeriod = period;
        Name = $"Smma({_period})";
        Init();
    }

    public Smma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer = new CircularBuffer(_period);
        _lastSmma = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _p_lastSmma = _lastSmma;
            _index++;
        }
        else
        {
            _lastSmma = _p_lastSmma;
        }
    }


    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer!.Add(Input.Value, Input.IsNew);

        double smma;

        if (_index <= _period)
        {
            smma = _buffer.Average();

            if (_index == _period)
            {
                _lastSmma = smma; // Initialize _lastSmma for the transition
            }
        }
        else
        {
            smma = ((_lastSmma * (_period - 1)) + Input.Value) / _period;
        }

        _lastSmma = smma;
        IsHot = _index >= WarmupPeriod;

        return smma;
    }
}