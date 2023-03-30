namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
Abstract classes with all scaffolding required to build indicators.
    All abstracts support period, NaN, and all permutations of Add() methods.
    Indicator classess need to implement:
        - Chaining constructor (Abstract's constructor executes first)
        - Default Add(value) class
        - optional Add(series) bulk insert class (for optimization of historical analysis)

    Single_TSeries_Indicator    - one single-value TSeries in, one TSeries out.
    Pair_TSeries_Indicator      - Two TSeries in, one TSeries out. (includes simple semaphoring)
    Single_TBars_Indicator      - One OHLCV TBars in, one TSeries out.

</summary> */
public abstract class Single_TSeries_Indicator : TSeries
{
  protected readonly int _period;
  protected readonly bool _NaN;
  protected readonly TSeries _data;
  protected int _p;

	// Chainable Constructor - add it at the end of primary constructor :base(source: source, period: period, useNaN: useNaN)
	protected Single_TSeries_Indicator(TSeries source, int period, bool useNaN) {
		_data = source;
		_period = period;
		_p = _period;
		_NaN = useNaN;
		_data.Pub += Sub;
	}

	// overridable Add() method to add/update a single item at the end of the list

	public virtual void Add((DateTime t, double v) TValue, bool update, bool useNaN) {
		if (_period == 0) { _p = Length; }
		var res = (TValue.t, Count < _p - 1 && _NaN ? double.NaN : TValue.v);
		base.Add(res, update);
	}
	public new virtual void Add((DateTime t, double v) TValue, bool update) => base.Add(TValue, update);

	// potentially overridable Add() method for the whole series (could be replaced with faster bulk algo)
	public virtual void Add(TSeries data) {
		foreach (var item in data) { Add(TValue: item, update: false); }
	}
	public new void Add((System.DateTime t, double v) TValue) => this.Add(TValue: TValue, update: false);
  public void Add(bool update) => this.Add(TValue: this._data[this._data.Count - 1], update: update);
  public void Add() => this.Add(TValue: this._data[this._data.Count - 1], update: false);
  public new void Sub(object source, TSeriesEventArgs e) => this.Add(TValue: this._data[this._data.Count - 1], update: e.update);

  protected static void Add_Replace(List<double> l, double v, bool update)
  {
    if (update)
    { l[l.Count - 1] = v; }
    else
    { l.Add(v); }
  }
  protected static double Add_Replace_Trim(List<double> l, double v, int p, bool update)
  {
    Add_Replace(l, v, update);
    double ret = (l.Count > 0) ? l.First() : 0;
    if (l.Count > p && p != 0)
    {
      l.RemoveAt(0);
    }
    return ret;
  }
}
