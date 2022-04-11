/**
ENTP: Entropy

Introduced by Claude Shannon in 1948, entropy measures the unpredictability
of the data, or equivalently, of its average information.

Calculation:
    P = close / SUM(close, length)
    E = SUM(-P * npLog(P) / npLog(base), length)

Sources:
    https://en.wikipedia.org/wiki/Entropy_(information_theory)
    https://math.stackexchange.com/questions/3428693/how-to-calculate-entropy-from-a-set-of-correlated-samples

**/
namespace QuantLib;
public class ENTP_Series : Single_TSeries_Indicator
{
  public ENTP_Series(TSeries source, int period, double logbase = 2.0, bool useNaN = false)  : base(source, period, useNaN) {
    this._logbase = logbase;
    if (base._data.Count > 0) { base.Add(base._data); }
  }
 protected double _logbase = 2.0;
  private readonly System.Collections.Generic.List<double> _buffer = new();
  private readonly System.Collections.Generic.List<double> _buff2 = new();

  public override void Add((System.DateTime t, double v) d, bool update)
  {
    if (update) { this._buffer[this._buffer.Count - 1] = d.v; }
        else { _buffer.Add(d.v); }
    if (_buffer.Count > this._p && this._p != 0) { _buffer.RemoveAt(0); }

    double _sum = 0;
    for (int i = 0; i < _buffer.Count; i++) { _sum += _buffer[i]; }
    
    double _pp = this._buffer[this._buffer.Count - 1] / _sum;
    double _ppp = -_pp * Math.Log(_pp) / Math.Log(_logbase);

    if (update) { this._buff2[this._buff2.Count - 1] = _ppp; }
        else { _buff2.Add(_ppp); }
    if (_buff2.Count > this._p && this._p != 0) { _buff2.RemoveAt(0); }

    double _entp = 0;
    for (int i = 0; i < _buff2.Count; i++) { _entp += _buff2[i]; }

    var result = (d.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _entp);
    base.Add(result, update);
  }
}