namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
ENTP: Entropy
    Introduced by Claude Shannon in 1948, entropy measures the unpredictability
    of the data, or equivalently, of its average information.

Calculation:
    P = close / Σ(close)
    ENTP = Σ(-P * Log(P) / Log(base))

Sources:
    https://en.wikipedia.org/wiki/Entropy_(information_theory)
    https://math.stackexchange.com/questions/3428693/how-to-calculate-entropy-from-a-set-of-correlated-samples

</summary> */

public class ENTROPY_Series : Single_TSeries_Indicator
{
    public ENTROPY_Series(TSeries source, int period, double logbase = 2.0, bool useNaN = false) : base(source, period, useNaN)
    {
        this._logbase = logbase;
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly double _logbase;
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly System.Collections.Generic.List<double> _buff2 = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sum = _buffer.Sum();
        
        double _pp = this._buffer[this._buffer.Count - 1] / _sum;
        double _ppp = -_pp * Math.Log(_pp) / Math.Log(this._logbase);

        Add_Replace_Trim(_buff2, _ppp, _p, update);
        double _entp = _buff2.Sum();

        base.Add((TValue.t, _entp), update, _NaN);
    }
}