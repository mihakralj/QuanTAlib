<<<<<<< Updated upstream:QuantLib/Series/HEMA_Series.cs
﻿using System;
namespace QuantLib;

/**
HEMA: Hull-EMA Moving Average
Modified HUll Moving Average; instead of using WMA (Weighted MA) for a
calculation, HEMA uses EMA for Hull's formula:

EMA1 = EMA(n/2) of price - where k = 4/(n/2 +1)
EMA2 = EMA(n) of price - where k = 3/(n+1)
Raw HMA = (2 * EMA1) - EMA2
EMA3 = EMA(sqrt(n)) of Raw HMA - where k = 2/(sqrt(n)+1)
**/

public class HEMA_Series : TSeries {
  private readonly int _p;
  private readonly bool _NaN;
  private readonly TSeries _data;
  private readonly double _k1, _k2, _k3;
  private double _lastema1, _lastlastema1;
  private double _lastema2, _lastlastema2;
  private double _lastema3, _lastlastema3;

  public HEMA_Series(TSeries source, int period, bool useNaN = false) {
    this._p = period;
    this._data = source;
    this._k1 = 4 / ((period * 0.5) + 1);
    this._k2 = 3 / (double)(period + 1);
    this._k3 = 2 / (Math.Sqrt(period) + 1);

    this._NaN = useNaN;
    this._lastema1 = this._lastlastema1 = double.NaN;
    this._lastema2 = this._lastlastema2 = double.NaN;
    this._lastema3 = this._lastlastema3 = double.NaN;
    source.Pub += this.Sub;
    if (source.Count > 0) {
      for (int i = 0; i < source.Count; i++) {
        this.Add(source[i], false);
      }
    }
  }

  public new void Add((System.DateTime t, double v)data, bool update = false) {
    if (update) {
      this._lastema1 = this._lastlastema1;
      this._lastema2 = this._lastlastema2;
      this._lastema3 = this._lastlastema3;
    }
    double _ema1 = System.Double.IsNaN(this._lastema1)
                       ? data.v
                       : data.v * this._k1 + this._lastema1 * (1 - this._k1);
    double _ema2 = System.Double.IsNaN(this._lastema2)
                       ? data.v
                       : data.v * this._k2 + this._lastema2 * (1 - this._k2);

    double _rawhema = (2 * _ema1) - _ema2;
    double _ema3 = System.Double.IsNaN(this._lastema3)
                       ? _rawhema
                       : _rawhema * this._k3 + this._lastema3 * (1 - this._k3);

    this._lastlastema1 = this._lastema1;
    this._lastlastema2 = this._lastema2;
    this._lastlastema3 = this._lastema3;
    this._lastema1 = _ema1;
    this._lastema2 = _ema2;
    this._lastema3 = _ema3;

    (System.DateTime t, double v) result =
        (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ema3);
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
=======
﻿using System;
namespace QuantLib;

/** 
HEMA: Hull-EMA Moving Average 
Modified HUll Moving Average; instead of using WMA (Weighted MA) for a calculation, HEMA uses EMA for Hull's
formula:

EMA1 = EMA(n/2) of price - where k = 4/(n/2 +1)
EMA2 = EMA(n) of price - where k = 3/(n+1)
Raw HMA = (2 * EMA1) - EMA2 
EMA3 = EMA(sqrt(n)) of Raw HMA - where k = 2/(sqrt(n)+1)
**/

public class HEMA_Series : TSeries
{
    private readonly int _p;
    private readonly bool _NaN;
    private readonly TSeries _data;
    private readonly double _k1, _k2, _k3;
    private double _lastema1, _lastlastema1;
    private double _lastema2, _lastlastema2;
    private double _lastema3, _lastlastema3;

    public HEMA_Series(TSeries source, int period, bool useNaN = false)
    {
        this._p = period;
        this._data = source;
        this._k1 = 4 / ((period * 0.5) + 1);
        this._k2 = 3 / (double)(period + 1);
        this._k3 = 2 / (Math.Sqrt(period) + 1);

        this._NaN = useNaN;
        this._lastema1 = this._lastlastema1 = double.NaN;
        this._lastema2 = this._lastlastema2 = double.NaN;
        this._lastema3 = this._lastlastema3 = double.NaN;
        source.Pub += this.Sub;
        if (source.Count > 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                this.Add(source[i], false);
            }
        }
    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update)
        {
            this._lastema1 = this._lastlastema1;
            this._lastema2 = this._lastlastema2;
            this._lastema3 = this._lastlastema3;
        }
        double _ema1 = System.Double.IsNaN(this._lastema1) ? data.v : data.v * this._k1 + this._lastema1 * (1 - this._k1);
        double _ema2 = System.Double.IsNaN(this._lastema2) ? data.v : data.v * this._k2 + this._lastema2 * (1 - this._k2);

        double _rawhema = (2 * _ema1) - _ema2;
        double _ema3 = System.Double.IsNaN(this._lastema3) ? _rawhema : _rawhema * this._k3 + this._lastema3 * (1 - this._k3);

        this._lastlastema1 = this._lastema1;
        this._lastlastema2 = this._lastema2;
        this._lastlastema3 = this._lastema3;
        this._lastema1 = _ema1;
        this._lastema2 = _ema2;
        this._lastema3 = _ema3;

        (System.DateTime t, double v) result = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ema3);
        if (update)
        {
            base[base.Count - 1] = result;
        }
        else
        {
            base.Add(result);
        }
    }

    public void Add(bool update = false)
    {
        this.Add(this._data[this._data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e) { this.Add(this._data[this._data.Count - 1], e.update); }

}
>>>>>>> Stashed changes:Algos/HEMA_Series.cs
