namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
SMAPE: Symmetric Mean Absolute Percentage Error
    Measures the size of the error in percentage terms

Sources:
  https://en.wikipedia.org/wiki/Symmetric_mean_absolute_percentage_error

</summary> */

public class SMAPE_Series : Single_TSeries_Indicator
{
    public SMAPE_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _smape = 0;
        for (int i = 0; i < _buffer.Count; i++) { _smape += Math.Abs(_buffer[i] - _sma) / (Math.Abs(_buffer[i]) + Math.Abs(_sma)); }
        _smape /= this._buffer.Count;

        base.Add((TValue.t, _smape), update, _NaN);
    }
}