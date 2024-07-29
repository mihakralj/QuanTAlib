namespace QuanTAlib;
using System;

/* <summary>
BIAS: Rate of change between the source and a moving average.
    Bias is a statistical term which means a systematic deviation from the actual value.

BIAS = (close - SMA) / SMA
       = (close / SMA) - 1

Sources:
	https://en.wikipedia.org/wiki/Bias_of_an_estimator

</summary> */

public class BIAS_Series : TSeries
{
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private readonly SMA_Series _sma;

    //core constructors
    public BIAS_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"BIAS({period})";
        _sma = new(period, false);
    }
    public BIAS_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public BIAS_Series() : this(period: 0, useNaN: false) { }
    public BIAS_Series(int period) : this(period: period, useNaN: false) { }
    public BIAS_Series(TBars source) : this(source.Close, 0, false) { }
    public BIAS_Series(TBars source, int period) : this(source.Close, period, false) { }
    public BIAS_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public BIAS_Series(TSeries source) : this(source, 0, false) { }
    public BIAS_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        var _s = _sma.Add(TValue, update);
        double _bias = (TValue.v / ((_s.v != 0) ? _s.v : 1)) - 1;

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _bias);
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
        _sma.Reset();
    }
}