namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
HMA: Hull Moving Average
    Developed by Alan Hull, an extremely fast and smooth moving average; almost
    eliminates lag altogether and manages to improve smoothing at the same time.

Sources:
    https://alanhull.com/hull-moving-average
    https://school.stockcharts.com/doku.php?id=technical_indicators:hull_moving_average

WMA1 = WMA(n/2) of price
WMA2 = WMA(n) of price
Raw HMA = (2 * WMA1) - WMA2
HMA = WMA(sqrt(n)) of Raw HMA

</summary> */

public class HMA_Series : TSeries
{
    protected int _period, _period2, _psqrt;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    protected WMA_Series _wma1, _wma2, _wma3;

    //core constructors
    public HMA_Series(int period, bool useNaN)
    {
        _period = period;
        _period2 = period / 2;
        _psqrt = (int)Math.Sqrt(period);
        _NaN = useNaN;
        _wma1 = new(Math.Max(_period2, 1), false);
        _wma2 = new(Math.Max(_period, 1), false);
        _wma3 = new(Math.Max(_psqrt, 1), useNaN);
        Name = $"HMA({period})";
    }
    public HMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public HMA_Series() : this(period: 0, useNaN: false) { }
    public HMA_Series(int period) : this(period: period, useNaN: false) { }
    public HMA_Series(TBars source) : this(source.Close, 0, false) { }
    public HMA_Series(TBars source, int period) : this(source.Close, period, false) { }
    public HMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public HMA_Series(TSeries source) : this(source, 0, false) { }
    public HMA_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        if (_period == 0)
        {
            _wma1.Len = this.Count / 2;
            _wma2.Len = this.Count;
            _wma1.Len = (int)Math.Sqrt(this.Count);
        }
        double _w1 = _wma1.Add(TValue, update).v;
        double _w2 = _wma2.Add(TValue, update).v;
        double _hma = _wma3.Add((2 * _w1) - _w2, update).v;
        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _hma);
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
        _wma1.Reset();
        _wma2.Reset();
        _wma3.Reset();
    }
}