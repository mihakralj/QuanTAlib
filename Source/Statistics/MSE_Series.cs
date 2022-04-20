namespace QuanTAlib;
using System;

/* <summary>
MSE: Mean Square Error 
    Defined as a Mean (Average) of the Square of the difference between actual and estimated values.

Sources:
  https://en.wikipedia.org/wiki/Mean_squared_error

</summary> */

public class MSE_Series : Single_TSeries_Indicator
{
    public MSE_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
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

        double _mse = 0;
        for (int i = 0; i < _buffer.Count; i++) { _mse += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
        _mse /= this._buffer.Count;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _mse);
        base.Add(result, update);
    }
}