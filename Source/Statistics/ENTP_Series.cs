namespace QuanTAlib;
using System;

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

public class ENTP_Series : Single_TSeries_Indicator
{
    public ENTP_Series(TSeries source, int period, double logbase = 2.0, bool useNaN = false) : base(source, period, useNaN)
    {
        this._logbase = logbase;
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly double _logbase;
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly System.Collections.Generic.List<double> _buff2 = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { this._buffer.Add(TValue.v); }
        if (this._buffer.Count > this._p && this._p != 0) { this._buffer.RemoveAt(0); }

        double _sum = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _sum += this._buffer[i]; }

        double _pp = this._buffer[this._buffer.Count - 1] / _sum;
        double _ppp = -_pp * Math.Log(_pp) / Math.Log(this._logbase);

        if (update) { this._buff2[this._buff2.Count - 1] = _ppp; }
        else { this._buff2.Add(_ppp); }
        if (this._buff2.Count > this._p && this._p != 0) { this._buff2.RemoveAt(0); }

        double _entp = 0;
        for (int i = 0; i < this._buff2.Count; i++) { _entp += this._buff2[i]; }

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _entp);
        base.Add(result, update);
    }
}