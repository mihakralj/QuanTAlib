namespace QuanTAlib;
using System;
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
    protected readonly int _p;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

    // Chainable Constructor - add it at the end of primary constructor :base(source: source, period: period, useNaN: useNaN)
    protected Single_TSeries_Indicator(TSeries source, int period, bool useNaN)
    {
        this._data = source;
        this._p = period;
        this._NaN = useNaN;
        this._data.Pub += this.Sub;
    }

    // overridable Add() method to add/update a single item at the end of the list
    public new virtual void Add((System.DateTime t, double v) TValue, bool update) => base.Add(TValue, update);

    // potentially overridable Add() method for the whole series (could be replaced with faster bulk algo)
    public virtual void Add(TSeries data)
    {
        for (int i = 0; i < data.Count; i++) { this.Add(TValue: data[i], update: false); }
    }

    public new void Add((System.DateTime t, double v) TValue) 
        => this.Add(TValue: TValue, update: false);
    public void Add(bool update) 
        => this.Add(TValue: this._data[this._data.Count - 1], update: update);
    public void Add() 
        => this.Add(TValue: this._data[this._data.Count - 1], update: false);
    public new void Sub(object source, TSeriesEventArgs e) 
        => this.Add(TValue: this._data[this._data.Count - 1], update: e.update);
}



public abstract class Pair_TSeries_Indicator : TSeries
{
    protected readonly TSeries _d1;
    protected readonly TSeries _d2;
    protected readonly double _dd1, _dd2;

  // Chainable Constructors - add them at the end of primary constructors if needed
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
    public virtual void Add((System.DateTime t, double v)TValue1, (System.DateTime t, double v)TValue2, bool update) 
        => base.Add(TValue: (TValue1.t, 0), update: update); // default inserts zeros


    // potentially overridable Add() bulk variations (could be replaced with faster bulk algos)
    public virtual void Add(TSeries d1, TSeries d2) {
        for (int i = 0; i < d1.Count; i++) { this.Add(d1[i], d2[i], update: false); }
    }
    public virtual void Add(TSeries d1, double dd2) {
        for (int i = 0; i < d1.Count; i++) { this.Add(d1[i], (d1[i].t, dd2), update: false); }
    }
    public virtual void Add(double dd1, TSeries d2) {
        for (int i = 0; i < d2.Count; i++) { this.Add((d2[i].t, dd1), d2[i], update: false); }
    }


    public void Add((System.DateTime t, double v)TValue1, (System.DateTime t, double v)TValue2) 
        => this.Add(TValue1, TValue2, update: false);

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

    public new void Sub(object source, TSeriesEventArgs e) 
        => this.Add(e.update);
}


public abstract class Single_TBars_Indicator : TSeries
{
    protected readonly int _p;
    protected readonly bool _NaN;
    protected readonly TBars _bars;

    // Chainable Constructor - add it at the end of primary constructor :base(source: source, period: period, useNaN: useNaN)
    protected Single_TBars_Indicator(TBars source, int period, bool useNaN)
    {
        this._p = period;
        this._bars = source;
        this._NaN = useNaN;
        this._bars.Pub += this.Sub;
    }

    // overridable Add() method to add/update a single item at the end of the list
    public virtual void Add((System.DateTime t, double o, double h, double l, double c, double v) TBar, bool update) => base.Add((TBar.t, 0.0), update);

    // potentially overridable Add() method for the whole bars or series (could be replaced with faster bulk algo)
    public virtual void Add(TBars bars)
    {
        for (int i = 0; i < bars.Count; i++) { this.Add(TBar: bars[i], update: false); }
    }

    public virtual void Add(TSeries data)
    {
	    for (int i = 0; i < data.Count; i++) { base.Add(TValue: data[i], update: false); }
    }

public new void Add((System.DateTime t, double o, double h, double l, double c, double v) TBar) 
        => this.Add(TBar: TBar, update: false);
    public void Add(bool update) 
        => this.Add(TBar: this._bars[this._bars.Count - 1], update: update);
    public void Add() 
        => this.Add(TBar: this._bars[this._bars.Count - 1], update: false);
    public new void Sub(object source, TSeriesEventArgs e) 
        => this.Add(TBar: this._bars[this._bars.Count - 1], update: e.update);
}
