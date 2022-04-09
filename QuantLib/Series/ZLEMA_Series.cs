using System;
namespace QuantLib;

/**
ZLEMA: Zero Lag Exponential Moving Average

The Zero lag exponential moving average (ZLEMA) indicator was created by John
Ehlers and Ric Way.

The formula for a given N-Day period and for a given Data series is:
Lag = (Period-1)/2
Ema Data = {Data+(Data-Data(Lag days ago))
ZLEMA = EMA (EmaData,Period)

The idea is do a regular exponential moving average (EMA) calculation but on a
de-lagged data instead of doing it on the regular data. Data is de-lagged by
removing the data from "lag" days ago thus removing (or attempting to remove)
the cumulative lag effect of the moving average.
**/

public class ZLEMA_Series : TSeries {
  private readonly int _p, _l;
  private readonly bool _NaN;
  private readonly TSeries _data;
  private readonly double _k;
  private readonly double _k1m;
  private double _lastema, _lastlastema;

  public ZLEMA_Series(TSeries source, int period, bool useNaN = false) {
    this._p = period;
    this._l = (int)Math.Round((this._p - 1) * 0.5);
    this._data = source;
    this._k = 2.0 / (double)(period + 1);
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

    double _lagdata =
        this._data[this._data.Count - 1].v +
        (this._data[this._data.Count - 1].v -
         this._data[Math.Max(this._data.Count - 1 - this._l, 0)].v);

    double _ema = System.Double.IsNaN(this._lastema)
                      ? _lagdata
                      : _lagdata * this._k + this._lastema * this._k1m;
    this._lastlastema = this._lastema;
    this._lastema = _ema;

    (System.DateTime t, double v) result =
        (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ema);
    base.Add(result, update);
  }

  public void Add(bool update = false) {
    this.Add(this._data[this._data.Count - 1], update);
  }
  public new void Sub(object source, TSeriesEventArgs e) {
    this.Add(this._data[this._data.Count - 1], e.update);
  }
}
