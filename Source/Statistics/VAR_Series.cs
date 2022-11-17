namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
VAR: Population Variance
  Population variance without Bessel's correction

Sources:
  https://en.wikipedia.org/wiki/Variance
  Bessel's correction: https://en.wikipedia.org/wiki/Bessel%27s_correction

Remark:
  VAR (Population Variance) is also known as a biased Sample Variance. For unbiased
  sample variance use SVAR instead.
    
</summary> */

public class VAR_Series : Single_TSeries_Indicator
{
    public VAR_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _pvar = 0;
        for (int i = 0; i < _buffer.Count; i++) { _pvar += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
        _pvar /= this._buffer.Count;

        base.Add((TValue.t, _pvar), update, _NaN);
    }
}