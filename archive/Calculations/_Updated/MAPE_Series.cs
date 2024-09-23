using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
MAPE: Mean Absolute Percentage Error
  Measures the size of the error in percentage terms

Calculation:
  MAPE = Σ(|close – SMA| / |close|) / n

Sources:
  https://en.wikipedia.org/wiki/Mean_absolute_percentage_error

Remark: 
  returns infinity if any of observations is 0. 
  Use SMAPE or WMAPE instead to avoid division-by-zero in MAPE

</summary> */

public class MAPE_Series : TSeries
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

    //core constructors
    public MAPE_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"MAPE({period})";
    }
    public MAPE_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public MAPE_Series() : this(period: 0, useNaN: false) { }
    public MAPE_Series(int period) : this(period: period, useNaN: false) { }
    public MAPE_Series(TBars source) : this(source.Close, 0, false) { }
    public MAPE_Series(TBars source, int period) : this(source.Close, period, false) { }
    public MAPE_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public MAPE_Series(TSeries source) : this(source, 0, false) { }
    public MAPE_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);

        double _sma = _buffer.Average();

        double _mape = 0;
        for (int i = 0; i < _buffer.Count; i++)
        {
            _mape += (_buffer[i] != 0) ? Math.Abs(_buffer[i] - _sma) / Math.Abs(_buffer[i]) : double.PositiveInfinity;
        }
        _mape /= (_buffer.Count > 0) ? _buffer.Count : 1;

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _mape);
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