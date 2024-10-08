namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
COVAR: Covariance
   Covariance is defined as the expected value (or mean) of the product
   of their deviations from their individual expected values.

Sources:
  https://en.wikipedia.org/wiki/Covariance

</summary> */


public class COVAR_Series : Pair_TSeries_Indicator {
    public COVAR_Series(TSeries d1, TSeries d2, int period, bool useNaN = false) : base(d1, d2, period, useNaN) {
        if (base._d1.Count > 0 && base._d2.Count > 0) {
            for (int i = 0; i < base._d1.Count; i++) {
                this.Add(base._d1[i], base._d2[i], false);
            }
        }
    }

    private readonly System.Collections.Generic.List<double> _x = new();
    private readonly System.Collections.Generic.List<double> _y = new();
    private readonly System.Collections.Generic.List<double> _xy = new();

    public override void Add((System.DateTime t, double v) TValue1, (System.DateTime t, double v) TValue2, bool update) {
        BufferTrim(_x, TValue1.v, _p, update);
        BufferTrim(_y, TValue2.v, _p, update);
        BufferTrim(_xy, TValue1.v * TValue2.v, _p, update);

        double _avgx = _x.Average();
        double _avgy = _y.Average();
        double _avgxy = _xy.Average();
        double _covar = _avgxy - (_avgx * _avgy);

        var result = (TValue1.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _covar);
        if (update) { base[base.Count - 1] = result; } else { base.Add(result); }
    }
}
