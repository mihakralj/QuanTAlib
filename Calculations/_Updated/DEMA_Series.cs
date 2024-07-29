namespace QuanTAlib;

using System;
using System.Linq;

/* <summary>
DEMA: Double Exponential Moving Average
    DEMA uses EMA(EMA()) to calculate smoother Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/double-exponential-moving-average-dema/

Remark:
    ema1 = EMA(close, length)
    ema2 = EMA(ema1, length)
    DEMA = 2 * ema1 - ema2

</summary> */

public class DEMA_Series : TSeries
{
    private double _k;
    private double _sum, _oldsum;
    private double _lastema1, _oldema1, _lastema2, _oldema2;
    private int _len;
    private readonly bool _useSMA;
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

    //core constructor
    public DEMA_Series(int period, bool useNaN, bool useSMA)
    {
        _period = period;
        _NaN = useNaN;
        _useSMA = useSMA;
        Name = $"DEMA({period})";
        _k = 2.0 / (_period + 1);
        _len = 0;
        _sum = _oldsum = _lastema1 = _lastema2 = 0;
    }
    //generic constructors (source)

    public DEMA_Series() : this(0, false, true) { }
    public DEMA_Series(int period) : this(period, false, true) { }
    public DEMA_Series(TBars source) : this(source.Close, 0, false) { }
    public DEMA_Series(TBars source, int period) : this(source.Close, period, false) { }
    public DEMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public DEMA_Series(TSeries source, int period) : this(source, period, false, true) { }
    public DEMA_Series(TSeries source, int period, bool useNaN) : this(source, period, useNaN, true) { }
    public DEMA_Series(TSeries source, int period, bool useNaN, bool useSMA) : this(period, useNaN, useSMA)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }

    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        if (update)
        {
            _lastema1 = _oldema1;
            _lastema2 = _oldema2;
            _sum = _oldsum;
        }
        else
        {
            _oldema1 = _lastema1;
            _oldema2 = _lastema2;
            _oldsum = _sum;
            _len++;
        }

        if (_period == 0)
        {
            _k = 2.0 / (_len + 1);
        }

        double _ema1, _ema2, _dema;
        if (Count == 0)
        {
            _ema1 = _ema2 = _sum = TValue.v;
        }
        else if (_len <= _period && _useSMA && _period != 0)
        {
            _sum += TValue.v;
            _ema1 = _sum / Math.Min(_len, _period);
            _ema2 = _ema1;
        }
        else
        {
            _ema1 = (TValue.v - _lastema1) * _k + _lastema1;
            _ema2 = (_ema1 - _lastema2) * _k + _lastema2;
        }

        _dema = 2 * _ema1 - _ema2;

        _lastema1 = double.IsNaN(_ema1) ? _lastema1 : _ema1;
        _lastema2 = double.IsNaN(_ema2) ? _lastema2 : _ema2;

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _dema);
        return base.Add(res, update);
    }

    //variation of Add()
    public override (DateTime t, double v) Add(TSeries data)
    {
        if (data == null)
        {
            return (DateTime.Today, double.NaN);
        }

        foreach (var item in data)
        {
            Add(item, false);
        }

        return _data.Last;
    }

    public (DateTime t, double v) Add(bool update)
    {
        return Add(_data.Last, update);
    }

    public (DateTime t, double v) Add()
    {
        return Add(_data.Last, false);
    }

    private new void Sub(object source, TSeriesEventArgs e)
    {
        Add(_data.Last, e.update);
    }

    //reset calculation
    public override void Reset()
    {
        _sum = _oldsum = _lastema1 = _lastema2 = 0;
        _len = 0;
    }
}