namespace QuanTAlib;
using System;

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
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { this._buffer.Add(TValue.v); }
        if (this._buffer.Count > this._p && this._p != 0) { this._buffer.RemoveAt(0); }

        double _sma = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _sma += this._buffer[i]; }
        _sma /= this._buffer.Count;

        double _svar = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _svar += (this._buffer[i] - _sma) * (this._buffer[i] - _sma); }
        _svar /= (this._buffer.Count > 1) ? this._buffer.Count - 1 : 1; // Bessel's correction

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _svar);
        base.Add(result, update);
    }
}