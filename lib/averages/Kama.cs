using System;

namespace QuanTAlib;

public class Kama : AbstractBase
{
    private readonly int _period;
    private readonly double _scFast, _scSlow;
    private CircularBuffer? _buffer;
    private double _lastKama, _p_lastKama;

    public Kama(int period, int fast = 2, int slow = 30) : base()
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _scFast = 2.0 / (((period < fast) ? period : fast) + 1);
        _scSlow = 2.0 / (slow + 1);
        WarmupPeriod = period;
        Name = $"Kama({_period}, {fast}, {slow})";
        Init();
    }

    public Kama(object source, int period, int fast = 2, int slow = 30) : this(period, fast, slow)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer = new CircularBuffer(_period + 1);
        _lastKama = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastKama = _lastKama;
        }
        else
        {
            _lastKama = _p_lastKama;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer!.Add(Input.Value, Input.IsNew);

        double kama;
        if (_index <= _period)
        {
            kama = Input.Value;
        }
        else
        {
            double change = Math.Abs(_buffer[^1] - _buffer[0]);
            double volatility = 0;
            for (int i = 1; i < _buffer.Count; i++)
            {
                volatility += Math.Abs(_buffer[i] - _buffer[i - 1]);
            }

            double er = volatility != 0 ? change / volatility : 0;
            double sc = (er * (_scFast - _scSlow)) + _scSlow;
            sc *= sc; // Square the smoothing constant

            kama = _lastKama + (sc * (Input.Value - _lastKama));
        }

        _lastKama = kama;
        IsHot = _index >= WarmupPeriod;

        return kama;
    }
}