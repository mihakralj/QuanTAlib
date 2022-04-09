namespace QuantLib;

/**
ENTRP: Entropy

Introduced by Claude Shannon in 1948, entropy measures the unpredictability
of the data, or equivalently, of its average information.

Sources:
    https://en.wikipedia.org/wiki/Entropy_(information_theory)
    https://math.stackexchange.com/questions/3428693/how-to-calculate-entropy-from-a-set-of-correlated-samples

**/

public class ENTP_Series : TSeries {
    private readonly int _p;
    private readonly TSeries _data;
    private readonly double _base;
    private readonly System.Collections.Generic.List<double> _b1 = new();
    private readonly System.Collections.Generic.List<double> _b2 = new();
  
    public ENTP_Series(TSeries source, int period, double basenum = 2.0) {
      this._p = period;
      this._base = basenum;
      this._data = source;
      
      source.Pub += this.Sub;
      if (source.Count > 0) {
        for (int i = 0; i < source.Count; i++) { this.Add(source[i], false); }
      }
    }
  
    public new void Add((System.DateTime t, double v)data, bool update = false) {
      if (update) {
        this._b1[this._b1.Count - 1] = data.v;
      } else {
        this._b1.Add(data.v);
      }

      if (this._b1.Count > this._p) {
        this._b1.RemoveAt(0);
      }

      double _sum=0;
      for (int i = 0; i < this._b1.Count; i++) { _sum += this._b1[i]; }
      double _v1 = data.v / _sum;
      double _v2 = -_v1 * Math.Log(_v1) / Math.Log(_base);

      if (update) {
        this._b2[this._b2.Count - 1] = _v2;
      } else {
        this._b2.Add(_v2);
      }
      if (this._b2.Count > this._p) {
        this._b2.RemoveAt(0);
      }
      double _ent=0;
      for (int i = 0; i < this._b2.Count; i++) { _ent += this._b2[i]; }

      (System.DateTime t, double v) result = (data.t, _ent);
      base.Add(result, update);
    }
  
    public void Add(bool update = false) {
      this.Add(this._data[this._data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e) {
      this.Add(this._data[this._data.Count - 1], e.update);
    }
  }