namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Numerics;

/* <summary>
T3: Tillson T3 Moving Average
    Tim Tillson described it in "Technical Analysis of Stocks and Commodities", January 1998 in the
    article "Better Moving Averages". Tillson’s moving average becomes a popular indicator of
    technical analysis as it gets less lag with the price chart and its curve is considerably smoother.

Sources:
    https://technicalindicators.net/indicators-technical-analysis/150-t3-moving-average
    http://www.binarytribune.com/forex-trading-indicators/t3-moving-average-indicator/
</summary> */

public class T3_Series : TSeries
{
    private readonly double _k, _k1m, _c1, _c2, _c3, _c4;
    private readonly System.Collections.Generic.List<double> _buffer1 = new();
    private readonly System.Collections.Generic.List<double> _buffer2 = new();
    private readonly System.Collections.Generic.List<double> _buffer3 = new();
    private readonly System.Collections.Generic.List<double> _buffer4 = new();
    private readonly System.Collections.Generic.List<double> _buffer5 = new();
    private readonly System.Collections.Generic.List<double> _buffer6 = new();
    private readonly bool _useSMA;
    private double _lastema1, _lastema2, _lastema3, _lastema4, _lastema5, _lastema6;
    private double _llastema1, _llastema2, _llastema3, _llastema4, _llastema5, _llastema6;
    protected int _len;
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

    //core constructors
    public T3_Series(int period, double vfactor, bool useSMA, bool useNaN)
    {
        _period = period;
        _len = 0;
        _NaN = useNaN;
        Name = $"T3({period})";
        _useSMA = useSMA;
        double _a = vfactor; //0.7; //0.618
        _c1 = -_a * _a * _a;
        _c2 = 3 * _a * _a + 3 * _a * _a * _a;
        _c3 = -6 * _a * _a - 3 * _a - 3 * _a * _a * _a;
        _c4 = 1 + 3 * _a + _a * _a * _a + 3 * _a * _a;

        _k = 2.0 / (_period + 1);
        _k1m = 1.0 - _k;
        _lastema1 = _llastema1 = _lastema2 = _llastema2 = _lastema3 = _llastema3 = _lastema4 = _llastema4 = _lastema5 = _llastema5 = _lastema5 = _llastema5 = 0;
    }
    public T3_Series(TSeries source, int period, double vfactor, bool useSMA, bool useNaN) : this(period, vfactor, useSMA, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public T3_Series() : this(period: 0, vfactor: 0.7, useSMA: true, useNaN: false) { }
    public T3_Series(int period) : this(period: period, vfactor: 0.7, useSMA: true, useNaN: false) { }
    public T3_Series(TBars source) : this(source.Close, 0, vfactor: 0.7, useSMA: true, useNaN: false) { }
    public T3_Series(TBars source, int period) : this(source.Close, period, vfactor: 0.7, useSMA: true, useNaN: false) { }
    public T3_Series(TBars source, int period, bool useNaN) : this(source.Close, period, vfactor: 0.7, useSMA: true, useNaN: useNaN) { }
    public T3_Series(TBars source, int period, double vfactor, bool useNaN) : this(source.Close, period, vfactor: vfactor, useSMA: true, useNaN: useNaN) { }
    public T3_Series(TBars source, int period, bool useSMA, bool useNaN) : this(source.Close, period, vfactor: 0.7, useSMA: useSMA, useNaN: useNaN) { }
    public T3_Series(TSeries source) : this(source, 0, vfactor: 0.7, useSMA: true, useNaN: false) { }
    public T3_Series(TSeries source, int period) : this(source: source, period: period, vfactor: 0.7, useSMA: true, useNaN: false) { }
    public T3_Series(TSeries source, int period, bool useNaN) : this(source: source, period: period, vfactor: 0.7, useSMA: true, useNaN: useNaN) { }
    public T3_Series(TSeries source, int period, double vfactor) : this(source: source, period: period, vfactor: vfactor, useSMA: true, useNaN: false) { }
    public T3_Series(TSeries source, int period, double vfactor, bool useNaN) : this(source: source, period: period, vfactor: vfactor, useSMA: true, useNaN: useNaN) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        double _ema1, _ema2, _ema3, _ema4, _ema5, _ema6;
        if (double.IsNaN(TValue.v))
        {
            return base.Add((TValue.t, Double.NaN), update);
        }

        if (update) { _lastema1 = _llastema1; _lastema2 = _llastema2; _lastema3 = _llastema3; _lastema4 = _llastema4; _lastema5 = _llastema5; _lastema6 = _llastema6; }
        else { _llastema1 = _lastema1; _llastema2 = _lastema2; _llastema3 = _lastema3; _llastema4 = _lastema4; _llastema5 = _lastema5; _llastema6 = _lastema6; }

        if (_len == 0) { _lastema1 = _lastema2 = _lastema3 = _lastema4 = _lastema5 = _lastema6 = TValue.v; }


        if ((_len < _period) && _useSMA)
        {
            BufferTrim(_buffer1, TValue.v, _period, update);
            _ema1 = 0;
            for (int i = 0; i < _buffer1.Count; i++) { _ema1 += _buffer1[i]; }
            _ema1 /= _buffer1.Count;

            BufferTrim(_buffer2, _ema1, _period, update);
            _ema2 = 0;
            for (int i = 0; i < _buffer2.Count; i++) { _ema2 += _buffer2[i]; }
            _ema2 /= _buffer2.Count;

            BufferTrim(_buffer3, _ema2, _period, update);
            _ema3 = 0;
            for (int i = 0; i < _buffer3.Count; i++) { _ema3 += _buffer3[i]; }
            _ema3 /= _buffer3.Count;

            BufferTrim(_buffer4, _ema3, _period, update);
            _ema4 = 0;
            for (int i = 0; i < _buffer4.Count; i++) { _ema4 += _buffer4[i]; }
            _ema4 /= _buffer4.Count;

            BufferTrim(_buffer5, _ema4, _period, update);
            _ema5 = 0;
            for (int i = 0; i < _buffer5.Count; i++) { _ema5 += _buffer5[i]; }
            _ema5 /= _buffer5.Count;

            BufferTrim(_buffer6, _ema5, _period, update);
            _ema6 = 0;
            for (int i = 0; i < _buffer6.Count; i++) { _ema6 += _buffer6[i]; }
            _ema6 /= _buffer6.Count;
        }
        else
        {
            _ema1 = (TValue.v * this._k) + (this._lastema1 * this._k1m);
            _ema2 = (_ema1 * this._k) + (this._lastema2 * this._k1m);
            _ema3 = (_ema2 * this._k) + (this._lastema3 * this._k1m);
            _ema4 = (_ema3 * this._k) + (this._lastema4 * this._k1m);
            _ema5 = (_ema4 * this._k) + (this._lastema5 * this._k1m);
            _ema6 = (_ema5 * this._k) + (this._lastema6 * this._k1m);
        }
        _len++;
        _lastema1 = _ema1;
        _lastema2 = _ema2;
        _lastema3 = _ema3;
        _lastema4 = _ema4;
        _lastema5 = _ema5;
        _lastema6 = _ema6;

        double _T3 = _c1 * _ema6 + _c2 * _ema5 + _c3 * _ema4 + _c4 * _ema3;
        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _T3);
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
        _lastema1 = _llastema1 = _lastema2 = _llastema2 = _lastema3 = _llastema3 = _lastema4 = _llastema4 = _lastema5 = _llastema5 = _lastema5 = _llastema5 = 0;
        _buffer1.Clear();
        _buffer2.Clear();
        _buffer3.Clear();
        _buffer4.Clear();
        _buffer5.Clear();
        _buffer6.Clear();
        _len = 0;
    }
}