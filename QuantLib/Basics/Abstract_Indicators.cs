namespace QuantLib;
using System;

public abstract class Single_TSeries_Indicator : TSeries
{
    protected readonly int _p;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

  // Default Constructor
  protected Single_TSeries_Indicator(TSeries source, int period, bool useNaN)
    {
        this._data = source;
        this._p = period;
        this._NaN = useNaN;
        this._data.Pub += this.Sub;
    }

    // overridable Add() method for the whole series (should be replaced with faster algo than default)
    public virtual void Add(TSeries data)
    {
        for (int i = 0; i < data.Count; i++)
        {
            this.Add(data[i], false);
        }
    }

    // overridable Add() method to add/update a single value at the end of the list
    public virtual new void Add((System.DateTime t, double v) tuple, bool update) => base.Add(tuple, update);

    // Add() without update parameter assumes this is an insert of new data (update=false)
    public new void Add((System.DateTime t, double v) d) => this.Add(d, update: false);

    // Add() without a tuple assumes add/update using the last item in the source as new data
    public void Add(bool update) => this.Add(this._data[this._data.Count - 1], update);

    // Add() without any parameters assumes this is an insert of new data using the last item in the source
    public void Add() => this.Add(this._data[this._data.Count - 1], update: false);

    // When event is triggered, call Add(bool update)
    public new void Sub(object source, TSeriesEventArgs e) => this.Add(this._data[this._data.Count - 1], e.update);
}