namespace QuanTAlib;

using System;
using System.Linq;

/* <summary>
ZL: Zero Lag
    Data is de-lagged by removing the data from “lag” days ago, thus removing
    (or attempting to) the cumulative effect of the moving average.

Calculation:
    Lag = (Period-1)/2
    ZL = Data + (Data - Data(Lag days ago) )

Sources:
     https://mudrex.com/blog/zero-lag-ema-trading-strategy/

</summary> */

public class ZL_Series : TSeries
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private int _len;
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private readonly EMA_Series _ema;

    //core constructor
    public ZL_Series(int period, bool useNaN, bool useSMA)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"ZL({period})";
        _len = 1;
        _ema = new(period);
    }
    //generic constructors (source)

    public ZL_Series() : this(0, false, true) { }
    public ZL_Series(int period) : this(period, false, true) { }
    public ZL_Series(TBars source) : this(source.Close, 0, false) { }
    public ZL_Series(TBars source, int period) : this(source.Close, period, false) { }
    public ZL_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public ZL_Series(TSeries source, int period) : this(source, period, false, true) { }
    public ZL_Series(TSeries source, int period, bool useNaN) : this(source, period, useNaN, true) { }
    public ZL_Series(TSeries source, int period, bool useNaN, bool useSMA) : this(period, useNaN, useSMA)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }

    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);
        int _lag;
        if (_period == 0)
        {
            _lag = (int)((_len - 1) * 0.5);
            _len++;
        }
        else { _lag = (int)((_period - 1) * 0.5); }
        _lag = Math.Min(_lag, _buffer.Count - 1);
        _lag = Math.Max(_lag, 0) + 1;
        double _zlValue = 2 * TValue.v - _buffer[^_lag];

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _zlValue);
        return base.Add(res, update);
    }

    //variation of Add()
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
        _buffer.Clear();
        _ema.Reset();
    }
}