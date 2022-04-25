namespace QuanTAlib;
using System;

/* <summary>
KURT: Kurtosis of population
  Kurtosis characterizes the relative peakedness or flatness of a distribution
  compared with the normal distribution. Positive kurtosis indicates a relatively
  peaked distribution. Negative kurtosis indicates a relatively flat distribution.

  The normal curve is called Mesokurtic curve. If the curve of a distribution is
  more outlier prone (or heavier-tailed) than a normal or mesokurtic curve then
  it is referred to as a Leptokurtic curve. If a curve is less outlier prone (or
  lighter-tailed) than a normal curve, it is called as a platykurtic curve.

Calculation:
    sum4 = Σ(close-SMA)^4
    sum2 = (Σ(close-SMA)^2)^2
    KURT = length * (sum4/sum2)

Sources:
  https://en.wikipedia.org/wiki/Kurtosis
  https://stats.oarc.ucla.edu/other/mult-pkg/faq/general/faq-whats-with-the-different-formulas-for-kurtosis/

</summary> */

public class KURT_Series : Single_TSeries_Indicator
{
    public KURT_Series(TSeries source, int period, double logbase = 2.0, bool useNaN = false) : base(source, period, useNaN)
    {
        this._logbase = logbase;
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    protected double _logbase;
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { this._buffer.Add(TValue.v); }
        if (this._buffer.Count > this._p && this._p != 0) { this._buffer.RemoveAt(0); }

        double _n = this._buffer.Count;

        double _avg = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _avg += this._buffer[i]; }
        _avg /= _n;

        double _s2 = 0;
        double _s4 = 0;
        for (int i = 0; i < this._buffer.Count; i++)
        {
            _s2 += (this._buffer[i] - _avg) * (this._buffer[i] - _avg);
            _s4 += (this._buffer[i] - _avg) * (this._buffer[i] - _avg) * (this._buffer[i] - _avg) * (this._buffer[i] - _avg);
        }

        double _Vx = _s2 / (_n - 1);
        double _kurt = (_n > 3) ? (((_n * (_n + 1)) / ((_n - 1) * (_n - 2) * (_n - 3))) * (_s4 / (_Vx * _Vx)) - (3 * ((_n - 1) * (_n - 1) / ((_n - 2) * (_n - 3))))) : Double.NaN;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? Double.NaN : _kurt);
        base.Add(result, update);
    }
}