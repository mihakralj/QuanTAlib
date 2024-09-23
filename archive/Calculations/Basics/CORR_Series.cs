namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
CORR: Pearson's Correlation Coefficient
  PCC is a measure of linear correlation between two sets of data.
  It is the ratio between the covariance of two variables and the product of
  their standard deviations; it is essentially a normalized measurement of
  the covariance, such that the result always has a value between −1 and 1.

Sources:
  https://en.wikipedia.org/wiki/Pearson_correlation_coefficient

</summary> */

public class CORR_Series : Pair_TSeries_Indicator
{
    public CORR_Series(TSeries d1, TSeries d2, int period, bool useNaN = false) : base(d1, d2, period, useNaN)
    {
        if (base._d1.Count > 0 && base._d2.Count > 0) { for (int i = 0; i < base._d1.Count; i++) { this.Add(base._d1[i], base._d2[i], false); } }
    }

    private readonly System.Collections.Generic.List<double> _x = new();
    private readonly System.Collections.Generic.List<double> _xx = new();
    private readonly System.Collections.Generic.List<double> _y = new();
    private readonly System.Collections.Generic.List<double> _yy = new();
    private readonly System.Collections.Generic.List<double> _xy = new();

    public override void Add((System.DateTime t, double v) TValue1, (System.DateTime t, double v) TValue2, bool update)
    {
        Add_Replace_Trim(_x, TValue1.v, _p, update);
        Add_Replace_Trim(_xx, TValue1.v * TValue1.v, _p, update);
        Add_Replace_Trim(_y, TValue2.v, _p, update);
        Add_Replace_Trim(_yy, TValue2.v * TValue2.v, _p, update);
        Add_Replace_Trim(_xy, TValue1.v * TValue2.v, _p, update);

        double _sumx = _x.Sum();
        double _sumxx = _xx.Sum();
        double _sumy = _y.Sum();
        double _sumyy = _yy.Sum();
        double _sumxy = _xy.Sum();

        double _covar = (_sumxx - _sumx * _sumx / _p) * (_sumyy - _sumy * _sumy / _p);
        double _cor = (_covar != 0) ? (_sumxy - _sumx * _sumy / _p) / Math.Sqrt(_covar) : 0.0;

        var result = (TValue1.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _cor);
        if (update) { base[base.Count - 1] = result; } else { base.Add(result); }

    }
}
