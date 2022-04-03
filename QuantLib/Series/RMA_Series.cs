namespace QuantLib;

/**
RMA: wildeR Moving Average
J. Welles Wilder introduced RMA as an alternative to EMA. RMA's weight (k) is
set as 1/period, giving less weight to the new data compared to EMA. Sources:
    https://tlc.thinkorswim.com/center/reference/Tech-Indicators/studies-library/V-Z/WildersSmoothing
    https://www.incrediblecharts.com/indicators/wilder_moving_average.php
Issues:
    Pandas-TA library calculates RMA using Exponential Weighted Mean:
pandas.ewm().mean() and returns incorrect first bar compared to published RMA
formula.
**/

public class RMA_Series : TSeries {
  private readonly int _p;
  private readonly bool _NaN;
  private readonly TSeries _data;
  private readonly double _k, _k1m;
  private double _lastema, _lastlastema;

  public RMA_Series(TSeries source, int period, bool useNaN = false) {
    this._p = period;
    this._data = source;
    this._k = 1.0 / (double)period;
    this._k1m = 1.0 - this._k;
    this._NaN = useNaN;
    this._lastema = this._lastlastema = double.NaN;
    source.Pub += this.Sub;
    if (source.Count > 0) {
      for (int i = 0; i < source.Count; i++) {
        this.Add(source[i], false);
      }
    }
  }

  public new void Add((System.DateTime t, double v)data, bool update = false) {
    if (update) {
      this._lastema = this._lastlastema;
    }
    double _ema = System.Double.IsNaN(this._lastema)
                      ? data.v
                      : data.v * this._k + this._lastema * this._k1m;
    this._lastlastema = this._lastema;
    this._lastema = _ema;

    (System.DateTime t, double v) result =
        (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ema);
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
