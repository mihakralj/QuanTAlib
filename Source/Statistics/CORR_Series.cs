namespace QuanTAlib;
using System;

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
    if (update)
    {
      _x[_x.Count - 1] = TValue1.v;
      _xx[_xx.Count - 1] = TValue1.v * TValue1.v;
      _y[_y.Count - 1] = TValue2.v;
      _y[_yy.Count - 1] = TValue2.v * TValue2.v;
      _xy[_xy.Count - 1] = TValue1.v * TValue2.v;
    }
    else
    {
      _x.Add(TValue1.v);
      _xx.Add(TValue1.v * TValue1.v);
      _y.Add(TValue2.v);
      _yy.Add(TValue2.v * TValue2.v);
      _xy.Add(TValue1.v * TValue2.v);
    }
    if (_x.Count > this._p) { _x.RemoveAt(0); }
    if (_xx.Count > this._p) { _xx.RemoveAt(0); }
    if (_y.Count > this._p) { _y.RemoveAt(0); }
    if (_yy.Count > this._p) { _yy.RemoveAt(0); }
    if (_xy.Count > this._p) { _xy.RemoveAt(0); }

    double _sumx = 0;
    for (int i = 0; i < _x.Count; i++) { _sumx += _x[i]; }
    double _sumxx = 0;
    for (int i = 0; i < _xx.Count; i++) { _sumxx += _xx[i]; }
    double _sumy = 0;
    for (int i = 0; i < _y.Count; i++) { _sumy += _y[i]; }
    double _sumyy = 0;
    for (int i = 0; i < _yy.Count; i++) { _sumyy += _yy[i]; }
    double _sumxy = 0;
    for (int i = 0; i < _xy.Count; i++) { _sumxy += _xy[i]; }

    double _div = (_sumxx - _sumx * _sumx / _p) * (_sumyy - _sumy * _sumy / _p);
    double _cor = (_div != 0) ? (_sumxy - _sumx * _sumy / _p) / Math.Sqrt(_div) : 0.0;

    var result = (TValue1.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _cor);
    if (update) { base[base.Count - 1] = result; } else { base.Add(result); }
  }
}