using System;

namespace QuanTAlib;

public class Frama : AbstractBase
{
    private readonly int _period;
    private readonly double _fc;
    private readonly CircularBuffer _buffer;
    private double _lastFrama;
    private double _prevLastFrama;

    public Frama(int period, double fc = 0.5)
    {
        if (period < 2)
            throw new ArgumentException("Period must be at least 2", nameof(period));

        _period = period;
        _fc = fc;
        _buffer = new CircularBuffer(period);
        WarmupPeriod = period;
    }

    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _lastFrama = 0;
        _prevLastFrama = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _prevLastFrama = _lastFrama;
            _index++;
        }
        else
        {
            _lastFrama = _prevLastFrama;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        if (_buffer.Count < _period)
        {
            _lastFrama = _buffer.Average();
            return _lastFrama;
        }

        int half = _period / 2;
        double hh = double.MinValue, ll = double.MaxValue;
        double hh1 = double.MinValue, ll1 = double.MaxValue;
        double hh2 = double.MinValue, ll2 = double.MaxValue;

        for (int i = 0; i < _period; i++)
        {
            double price = _buffer[i];
            hh = Math.Max(hh, price);
            ll = Math.Min(ll, price);

            if (i < half)
            {
                hh1 = Math.Max(hh1, price);
                ll1 = Math.Min(ll1, price);
            }
            else
            {
                hh2 = Math.Max(hh2, price);
                ll2 = Math.Min(ll2, price);
            }
        }

        double n1 = (hh - ll) / _period;
        double n2 = (hh1 - ll1 + hh2 - ll2) / (_period / 2);

        double d = (Math.Log(n2 + double.Epsilon) - Math.Log(n1 + double.Epsilon)) / Math.Log(2);

        double alpha = Math.Exp(-4.6 * (d - 1));
        alpha = Math.Max(Math.Min(alpha, 1), 0.01);  // Ensure alpha is between 0.01 and 1

        _lastFrama = alpha * (Input.Value - _lastFrama) + _lastFrama;

        IsHot = _index >= WarmupPeriod;
        return _lastFrama;
    }

    protected override double GetLastValid()
    {
        return _lastFrama;
    }
}
