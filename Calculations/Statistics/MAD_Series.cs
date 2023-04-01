namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
MAD: Mean Absolute Deviation
  Also known as AAD - Average Absolute Deviation, to differentiate it from Median Absolute Deviation
  MAD defines the degree of variation across the series.

Calculation:
  MAD = Σ(|close-SMA|) / period

Sources:
  https://en.wikipedia.org/wiki/Average_absolute_deviation
   
</summary> */

public class MAD_Series : Single_TSeries_Indicator
{
    public MAD_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);

        double _sma = _buffer.Average();

        double _mad = 0;
        for (int i = 0; i < _buffer.Count; i++) { _mad += Math.Abs(_buffer[i] - _sma); }
        _mad /= this._buffer.Count;

        base.Add((TValue.t, _mad), update, _NaN);
    }
}