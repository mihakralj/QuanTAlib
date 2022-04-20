namespace QuanTAlib;
using System;

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

public class MAPE_Series : Single_TSeries_Indicator
{
    public MAPE_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { _buffer[_buffer.Count - 1] = TValue.v; }
        else { _buffer.Add(TValue.v); }
        if (_buffer.Count > this._p && this._p != 0) { _buffer.RemoveAt(0); }

        double _sma = 0;
        for (int i = 0; i < _buffer.Count; i++) { _sma += _buffer[i]; }
        _sma /= this._buffer.Count;

        double _mape = 0;
        for (int i = 0; i < _buffer.Count; i++) { _mape += (_buffer[i] != 0) ? Math.Abs(_buffer[i] - _sma) / Math.Abs(_buffer[i]) : double.PositiveInfinity; }
        _mape /= this._buffer.Count;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _mape);
        base.Add(result, update);
    }
}