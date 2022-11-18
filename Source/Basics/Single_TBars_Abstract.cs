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
    public virtual void Add((System.DateTime t, double v) TValue, bool update, bool useNaN)
    {
        var res = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : TValue.v);
        base.Add(res, update);
    }

    // potentially overridable Add() method for the whole bars or series (could be replaced with faster bulk algo)
    public virtual void Add(TBars bars) { for (int i = 0; i < bars.Count; i++) { this.Add(TBar: bars[i], update: false); }}
    public virtual void Add(TSeries data) { for (int i = 0; i < data.Count; i++) { base.Add(TValue: data[i], update: false); }}
    public void Add((System.DateTime t, double o, double h, double l, double c, double v) TBar) => this.Add(TBar: TBar, update: false);
    public void Add(bool update) => this.Add(TBar: this._bars[this._bars.Count - 1], update: update);
    public void Add() => this.Add(TBar: this._bars[this._bars.Count - 1], update: false);
    public new void Sub(object source, TSeriesEventArgs e) => this.Add(TBar: this._bars[this._bars.Count - 1], update: e.update);

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
