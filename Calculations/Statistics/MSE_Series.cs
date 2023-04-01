namespace QuanTAlib;
using System;
using System.Linq;

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
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _mse = 0;
        for (int i = 0; i < _buffer.Count; i++) { _mse += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
        _mse /= this._buffer.Count;

        base.Add((TValue.t, _mse), update, _NaN);
    }
}