namespace QuanTAlib;
using System;
using System.Linq;

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
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _mape = 0;
        for (int i = 0; i < _buffer.Count; i++) { 
            _mape += (_buffer[i] != 0) ? Math.Abs(_buffer[i] - _sma) / Math.Abs(_buffer[i]) : double.PositiveInfinity; 
        }
        _mape /= (_buffer.Count>0) ? _buffer.Count : 1;

        base.Add((TValue.t, _mape), update, _NaN);
    }
}