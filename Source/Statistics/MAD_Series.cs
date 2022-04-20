namespace QuanTAlib;
using System;

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
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { _buffer.Add(TValue.v); }
        if (_buffer.Count > this._p && this._p != 0) { _buffer.RemoveAt(0); }

        double _sma = 0;
        for (int i = 0; i < _buffer.Count; i++) { _sma += _buffer[i]; }
        _sma /= this._buffer.Count;

        double _mad = 0;
        for (int i = 0; i < _buffer.Count; i++) { _mad += Math.Abs(_buffer[i] - _sma); }
        _mad /= this._buffer.Count;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _mad);
        base.Add(result, update);
    }
}