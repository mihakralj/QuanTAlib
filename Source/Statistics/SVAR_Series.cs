namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
SVAR: Sample Variance
  Sample variance uses Bessel's correction to correct the bias in the estimation of population variance.

Sources:
  https://en.wikipedia.org/wiki/Variance
  Bessel's correction: https://en.wikipedia.org/wiki/Bessel%27s_correction

Remark:
  SVAR is also known as the Unbiased Sample Variance, while VAR (Population Variance) is known as
  the Biased Sample Variance. 
    
</summary> */

public class SVAR_Series : Single_TSeries_Indicator
{
    public SVAR_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _svar = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _svar += (this._buffer[i] - _sma) * (this._buffer[i] - _sma); }
        _svar /= (this._buffer.Count > 1) ? this._buffer.Count - 1 : 1; // Bessel's correction

        base.Add((TValue.t, _svar), update, _NaN);
    }
}