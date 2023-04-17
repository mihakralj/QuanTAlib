namespace QuanTAlib;
using System;
using System.Collections.Generic;

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


public abstract class Pair_TSeries_Indicator : TSeries
{
    protected readonly int _p;
    protected readonly bool _NaN;
    protected readonly TSeries _d1;
    protected readonly TSeries _d2;
    protected readonly double _dd1, _dd2;

  // Chainable Constructors - add them at the end of primary constructors if needed
    protected Pair_TSeries_Indicator(TSeries source1, TSeries source2, int period, bool useNaN)
    {
        this._p = period;
        this._NaN = useNaN;
        this._d1 = source1;
        this._d2 = source2;
        this._dd1 = double.NaN;
        this._dd2 = double.NaN;
        this._d1.Pub += this.Sub;
        this._d2.Pub += this.Sub;
    }
  protected Pair_TSeries_Indicator(TSeries source1, TSeries source2)
    {
        this._d1 = source1;
        this._d2 = source2;
        this._dd1 = double.NaN;
        this._dd2 = double.NaN;
        this._d1.Pub += this.Sub;
        this._d2.Pub += this.Sub;
    }
    protected Pair_TSeries_Indicator(TSeries source1, double dd2)
    {
        this._d1 = source1;
        this._d2 = new();
        this._dd1 = double.NaN;
        this._dd2 = dd2;
        this._d1.Pub += this.Sub;
    }
        protected Pair_TSeries_Indicator(double dd1, TSeries source2)
    {
        this._d1 = new();
        this._d2 = source2;
        this._dd1 = dd1;
        this._dd2 = double.NaN;
        this._d2.Pub += this.Sub;
    }

    // overridable Add(Tvalue, Tvalue) method to add/update a single value at the end of the list
    public virtual void Add((System.DateTime t, double v)TValue1, (System.DateTime t, double v)TValue2, bool update) => base.Add(TValue: (TValue1.t, 0), update: update); // default inserts zeros

    // potentially overridable Add() bulk variations (could be replaced with faster bulk algos)
    public virtual void Add(TSeries d1, TSeries d2) { for (int i = 0; i < d1.Count; i++) { this.Add(d1[i], d2[i], update: false); }}
    public virtual void Add(TSeries d1, double dd2) { for (int i = 0; i < d1.Count; i++) { this.Add(d1[i], (d1[i].t, dd2), update: false); }}
    public virtual void Add(double dd1, TSeries d2) { for (int i = 0; i < d2.Count; i++) { this.Add((d2[i].t, dd1), d2[i], update: false); }}

    public void Add((System.DateTime t, double v)TValue1, (System.DateTime t, double v)TValue2) => this.Add(TValue1, TValue2, update: false);

	public void Add(bool update)
	{
		if ((this._dd1 is double.NaN) && (this._dd2 is double.NaN))
		{
			// (Series, Series)
			if (update || (this._d1.Count > this.Count && this._d2.Count > this.Count))
			{ this.Add(this._d1[this._d1.Count - 1], this._d2[this._d2.Count - 1], update); }
		}
		else if ((this._dd2 is not double.NaN) && (this._dd1 is double.NaN))
		{
			// (Series, Double)
			this.Add(TValue1: this._d1[this._d1.Count - 1], TValue2: (this._d1[this._d1.Count - 1].t, this._dd2), update: update);
		}
		else
		{
			// (Double, Series)
			this.Add(TValue1: (this._d2[this._d2.Count - 1].t, this._dd1), TValue2: this._d2[this._d2.Count - 1], update: update);
		}
	}

	public void Add() => this.Add(update: false);
    public new void Sub(object source, TSeriesEventArgs e) => this.Add(e.update);

    protected static void Add_Replace(List<double> l, double v, bool update)
    {
        if (update)
        { l[l.Count - 1] = v; }
        else
        { l.Add(v); }
    }
    protected static void Add_Replace_Trim(List<double> l, double v, int p, bool update)
    {
        Add_Replace(l, v, update);
        if (l.Count > p && p != 0)
        { l.RemoveAt(0); }
    }
}
