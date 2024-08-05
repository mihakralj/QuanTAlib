using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
SDEV: Population Standard Deviation
  Population Standard Deviation is the square root of the biased variance, also knons as
  Uncorrected Sample Standard Deviation

Sources:
  https://en.wikipedia.org/wiki/Standard_deviation#Uncorrected_sample_standard_deviation

Remark:
  SDEV (Population Standard Deviation) is also known as a biased/uncorrected Standard Deviation.
  For unbiased version that uses Bessel's correction, use SDEV instead.

</summary> */

public class SDEV_Series : TSeries
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

    //core constructors
    public SDEV_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"SDEV({period})";
    }
    public SDEV_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public SDEV_Series() : this(period: 0, useNaN: false) { }
    public SDEV_Series(int period) : this(period: period, useNaN: false) { }
    public SDEV_Series(TBars source) : this(source.Close, 0, false) { }
    public SDEV_Series(TBars source, int period) : this(source.Close, period, false) { }
    public SDEV_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public SDEV_Series(TSeries source) : this(source, 0, false) { }
    public SDEV_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);

        double _sma = _buffer.Average();

        double _var = 0;
        for (int i = 0; i < _buffer.Count; i++) { _var += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
        _var /= this._buffer.Count;
        double _sdev = Math.Sqrt(_var);

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _sdev);
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