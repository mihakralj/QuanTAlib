using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
ZSCORE: number of standard deviations from SMA
  Z-score describes a value's relationship to the mean of a series, as measured in
  terms of standard deviations from the mean. If a Z-score is 0, it indicates that
  the data point's score is identical to the mean score. A Z-score of 1.0 would
  indicate a value that is one standard deviation from the mean. Z-scores may be
  positive or negative, with a positive value indicating the score is above the
  mean and a negative score indicating it is below the mean.

Sources:
  https://en.wikipedia.org/wiki/Z-score
  https://www.investopedia.com/terms/z/zscore.asp

Calculation:
    std = std * STDEV(close, length)
    mean = SMA(close, length)
    ZSCORE = (close - mean) / std

</summary> */

public class ZSCORE_Series : TSeries
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

    //core constructors
    public ZSCORE_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"ZSCORE({period})";
    }
    public ZSCORE_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public ZSCORE_Series() : this(period: 0, useNaN: false) { }
    public ZSCORE_Series(int period) : this(period: period, useNaN: false) { }
    public ZSCORE_Series(TBars source) : this(source.Close, 0, false) { }
    public ZSCORE_Series(TBars source, int period) : this(source.Close, period, false) { }
    public ZSCORE_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public ZSCORE_Series(TSeries source) : this(source, 0, false) { }
    public ZSCORE_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);
        double _sma = _buffer.Average();

        double _pvar = 0;
        for (int i = 0; i < _buffer.Count; i++) { _pvar += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
        _pvar /= this._buffer.Count;
        double _psdev = Math.Sqrt(_pvar);
        double _zscore = (_psdev == 0) ? 1 : (TValue.v - _sma) / _psdev;

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _zscore);
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
        _buffer.Clear();
    }
}