namespace QuantLib;

/**
WMA: (linearly) Weighted Moving Average
The weights are linearly decreasing over the period and the most recent data has
the heaviest weight.

Sources:
    https://corporatefinanceinstitute.com/resources/knowledge/trading-investing/weighted-moving-average-wma/
    https://www.technicalindicators.net/indicators-technical-analysis/83-moving-averages-simple-exponential-weighted
**/

public class WMA_Series : TSeries {
  private readonly int _p;
  private readonly bool _NaN;
  private readonly TSeries _data;
  private readonly System.Collections.Generic.List<double> _buffer = new();
  private readonly System.Collections.Generic.List<double> _weights = new();

  public WMA_Series(TSeries source, int period, bool useNaN = false) {
    this._p = period;
    this._data = source;
    this._NaN = useNaN;
    for (int i = 0; i < this._p; i++) {
      this._weights.Add(i + 1);
    }

    source.Pub += this.Sub;
    if (source.Count > 0) {
      for (int i = 0; i < source.Count; i++) {
        this.Add(source[i], false);
      }
    }
  }
  public new void Add((System.DateTime t, double v)data, bool update = false) {
    if (update) {
      this._buffer[this._buffer.Count - 1] = data.v;
    } else {
      this._buffer.Add(data.v);
    }
    if (this._buffer.Count > this._p) {
      this._buffer.RemoveAt(0);
    }

    double _wma = 0;
    for (int i = 0; i < this._buffer.Count; i++) {
      _wma += this._buffer[i] * this._weights[i];
    }
    _wma /= (this._buffer.Count * (this._buffer.Count + 1)) * 0.5;

    (System.DateTime t, double v) result =
        (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _wma);
    if (update) {
      base[base.Count - 1] = result;
    } else {
      base.Add(result);
    }
  }
  public void Add(bool update = false) {
    this.Add(this._data[this._data.Count - 1], update);
  }
  public new void Sub(object source, TSeriesEventArgs e) {
    this.Add(this._data[this._data.Count - 1], e.update);
  }
}