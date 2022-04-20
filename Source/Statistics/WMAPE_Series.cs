namespace QuanTAlib; 
using System;

/* <summary>
WMAPE: Weighted Mean Absolute Percentage Error
    Measures the size of the error in percentage terms

Sources:
  https://en.wikipedia.org/wiki/WMAPE

</summary> */

public class WMAPE_Series : Single_TSeries_Indicator
{
    public WMAPE_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
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

        double _div = 0;
        double _wmape = 0;
        for (int i = 0; i < _buffer.Count; i++)
        {
            _wmape += Math.Abs(_buffer[i] - _sma);
            _div += Math.Abs(_buffer[i]);
        }
        _wmape /= _div;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _wmape);
        base.Add(result, update);
    }
}