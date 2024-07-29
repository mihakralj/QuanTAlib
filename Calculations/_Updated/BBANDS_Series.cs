namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
BBANDS: Bollinger Bands®
    Price channels created by John Bollinger, depict volatility as standard deviation boundary 
    line range from a moving average of price. The bands automatically widen when volatility 
    increases and contract when volatility decreases. Their dynamic nature allows them to be 
    used on different securities with the standard settings.

    Mid Band = simple moving average (SMA)
    Upper Band = SMA + (standard deviation of price x multiplier) 
    Lower Band = SMA - (standard deviation of price x multiplier)
    Bandwidth = Width of the channel: (Upper-Lower)/SMA
    %B = The location of the data point within the channel: (Price-Lower)/(Upper/Lower)
    Z-Score = number of standard deviations of the data point from SMA

Sources:
    https://www.investopedia.com/terms/b/bollingerbands.asp
    https://school.stockcharts.com/doku.php?id=technical_indicators:bollinger_bands

Note:
    Bollinger Bands® is a registered trademark of John A. Bollinger.

</summary> */

public class BBANDS_Series : TSeries
{
    protected readonly int _period;
    protected readonly double _multiplier;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    public SMA_Series Mid { get; }
    public TSeries Upper { get; }
    public TSeries Lower { get; }
    public TSeries PercentB { get; }
    public TSeries Bandwidth { get; }
    public TSeries Zscore { get; }
    private readonly SDEV_Series _sdev;

    //core constructors
    public BBANDS_Series(int period, double multiplier, bool useNaN)
    {
        _period = period;
        _multiplier = multiplier;
        _NaN = useNaN;
        Name = $"BBANDS({period})";
    }
    public BBANDS_Series(TSeries source, int period, double multiplier, bool useNaN) : this(period, multiplier, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        Upper = new("BB_Up");
        Lower = new("BB_Low");
        Bandwidth = new("BBandwidth");
        PercentB = new("%BBandwidth");
        Zscore = new("Zscore");

        Mid = new(period, false);
        _sdev = new(period, false);

        _data.Pub += Sub;
        Add(_data);
    }

    public BBANDS_Series() : this(period: 0, multiplier: 2.0, useNaN: false) { }
    public BBANDS_Series(int period) : this(period: period, multiplier: 2.0, useNaN: false) { }
    public BBANDS_Series(TBars source) : this(source: source.Close, period: 0, multiplier: 2.0, useNaN: false) { }
    public BBANDS_Series(TBars source, int period) : this(source: source.Close, period: period, multiplier: 2.0, useNaN: false) { }
    public BBANDS_Series(TBars source, int period, double multiplier, bool useNaN) : this(source.Close, period: period, multiplier: multiplier, useNaN: false) { }
    public BBANDS_Series(TSeries source) : this(source, period: 0, useNaN: false) { }
    public BBANDS_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }
    public BBANDS_Series(TSeries source, int period, bool useNaN) : this(source: source, period: period, multiplier: 2.0, useNaN: useNaN) { }

    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        var _mid = Mid.Add(TValue, update);
        var _sd = this._sdev.Add(TValue, update);
        var _upper = Upper.Add((TValue.t, _mid.v + _sd.v * _multiplier), update);
        var _lower = Lower.Add((TValue.t, _mid.v - _sd.v * _multiplier), update);
        double _pbdnd = TValue.v - _lower.v;
        double _pbdvr = _upper.v - _lower.v;
        PercentB.Add((TValue.t, _pbdnd / _pbdvr), update);
        Zscore.Add((TValue.t, (TValue.v - _mid.v) / _sd.v), update);
        Bandwidth.Add((TValue.t, _pbdvr / _mid.v), update);

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _pbdvr / _mid.v);
        return base.Add(res, update);
    }

    //variation of Add()
    public override (DateTime t, double v) Add(TSeries data)
    {
        if (data == null) { return (DateTime.Today, Double.NaN); }
        foreach (var item in data) { Add(item); }
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
        Mid.Clear();
        _sdev.Clear();
        Upper.Clear();
        Lower.Clear();
    }
}