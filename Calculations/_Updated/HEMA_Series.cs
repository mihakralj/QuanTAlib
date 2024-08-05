namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
HEMA: Hull-EMA Moving Average - a hybrid indicator
    Modified HUll Moving Average; instead of using WMA (Weighted MA) for calculation,
    HEMA uses EMA for Hull's formula:

EMA1 = EMA(n/2) of price - where k = 4/(n/2 +1)
EMA2 = EMA(n) of price - where k = 3/(n+1)
Raw HMA = (2 * EMA1) - EMA2
EMA3 = EMA(sqrt(n)) of Raw HMA - where k = 2/(sqrt(n)+1)
</summary> */

public class HEMA_Series : TSeries
{
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private double _k1, _k2, _k3;
    private int _len;
    private double _lastema1, _oldema1;
    private double _lastema2, _oldema2;
    private double _lasthema, _oldhema;

    //core constructors
    public HEMA_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"HEMA({period})";
        (_k1, _k2, _k3) = CalculateK(_period);
        _len = 0;
        _lastema1 = _oldema1 = _lastema2 = _oldema2 = _lasthema = _oldhema = 0;
    }
    public HEMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public HEMA_Series() : this(period: 0, useNaN: false) { }
    public HEMA_Series(int period) : this(period: period, useNaN: false) { }
    public HEMA_Series(TBars source) : this(source.Close, 0, false) { }
    public HEMA_Series(TBars source, int period) : this(source.Close, period, false) { }
    public HEMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public HEMA_Series(TSeries source) : this(source, 0, false) { }
    public HEMA_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        if (update)
        {
            _lastema1 = _oldema1;
            _lastema2 = _oldema2;
            _lasthema = _oldhema;
        }
        else
        {
            _oldema1 = _lastema1;
            _oldema2 = _lastema2;
            _oldhema = _lasthema;
        }
        double _ema1, _ema2, _hema;
        if (_period == 0)
        {
            _len++;
            (_k1, _k2, _k3) = CalculateK(_len);
        }
        if (double.IsNaN(TValue.v))
        {
            return base.Add((TValue.t, double.NaN), update);
        }
        else if (this.Count == 0)
        {
            _ema1 = _ema2 = _hema = TValue.v;
        }
        else
        {
            _ema1 = _k1 * (TValue.v - _lastema1) + _lastema1;
            _ema2 = _k2 * (TValue.v - _lastema2) + _lastema2;
            _hema = _k3 * (((2 * _ema1) - _ema2) - _lasthema) + _lasthema;
        }

        _lastema1 = _ema1;
        _lastema2 = _ema2;
        _lasthema = _hema;

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _hema);
        return base.Add(res, update);
    }

    public override (DateTime t, double v) Add(TSeries data)
    {
        if (data == null) { return (DateTime.Today, Double.NaN); }
        foreach (var item in data) { Add(item, false); }
        return _data.Last;
    }
    public (DateTime t, double v) Add(bool update)
    {
        return this.Add(TValue: _data.Last, update: update);
    }
    public (DateTime t, double v) Add()
    {
        return Add(TValue: _data.Last, update: false);
    }
    private new void Sub(object source, TSeriesEventArgs e)
    {
        Add(TValue: _data.Last, update: e.update);
    }

    //reset calculation
    public override void Reset()
    {
        _lastema1 = _lastema2 = _lasthema = 0;
        _oldema1 = _oldema2 = _oldhema = 0;
        _len = 0;
    }

    public static (double k1, double k2, double k3) CalculateK(int len)
    {
        double k1 = 8 / (double)(len + 7);
        double k2 = 3 / (double)(len + 2);
        double k3 = 2 / Math.Sqrt(len + 3);

        return (k1, k2, k3);
    }

}