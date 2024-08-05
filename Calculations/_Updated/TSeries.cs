namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

/* <summary>
TSeries is the cornerstone of all QuanTAlib classes.
    TSeries is a single List of tuples (time, value) and contains several operators, casts, overloads
    and other helpers that simplify usage of library.
    Think of TSeries as an equivalent of Numpy array.

        - includes Length property (to mimic array's method)
        - includes publishing and subscribing methods that attach to events

</summary> */
public class TSeriesEventArgs : EventArgs
{
    public bool update { get; set; }
}

public class TSeries : List<(DateTime t, double v)>
{
    private readonly (DateTime t, double v) Default = (DateTime.MinValue, double.NaN);
    public IEnumerable<DateTime> t => this.Select(item => item.t);
    public IEnumerable<double> v => this.Select(item => item.v);
    public (DateTime t, double v) Last => Count > 0 ? this[^1] : Default;

    public int Length => Count;
    public string Name { get; set; }
    public int Keep = 0;

    public TSeries()
    {
        this.Name = "data";
    }

    public TSeries(string Name)
    {
        this.Name = Name;
    }

    public virtual (DateTime t, double v) Add(double v, bool update = false)
    {
        return Add((t: Count == 0 ? DateTime.Today : this[^1].t.AddDays(1), v), update);
    }

    public virtual (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        if (update)
        {
            this[^1] = TValue;
        }
        else
        {
            base.Add(TValue);
        }

        OnEvent(update);
        return TValue;
    }

    public virtual (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false)
    {
        if (update)
        {
            this[this.Count - 1] = (TBar.t, TBar.c);
        }
        else
        {
            base.Add((TBar.t, TBar.c));
        }

        OnEvent(update);
        return (TBar.t, TBar.c);
    }

    public virtual (DateTime t, double v) Add(TSeries data)
    {
        foreach (var item in data) { Add(item); }
        return data.Last;
    }

    public virtual (DateTime t, double v) Add(TBars data)
    {
        foreach (var item in data) { Add(item.c, false); }
        return (data.Last.t, data.Last.c);
    }

    public void Sub(object source, TSeriesEventArgs e)
    {
        var data = (TSeries)source;
        if (data == null) { return; }
        foreach (var item in data) { Add(item); }
    }

    public delegate void NewEventHandler(object source, TSeriesEventArgs args);

    public event NewEventHandler Pub;

    protected virtual void OnEvent(bool update = false)
    {
        if (Keep > 0)
        {
            TrimToSize(keep: Keep);
        }
        Pub?.Invoke(this, new TSeriesEventArgs { update = update });
    }

    /// common helpers
    public static void BufferTrim(List<double> buffer, double value, int period, bool update)
    {
        if (!update)
        {
            buffer.Add(value);
            if (buffer.Count > period && period > 0) { buffer.RemoveAt(0); }
            return;
        }
        buffer[^1] = value;
    }
    public virtual void Reset()
    {
    }

    public void TrimToSize(int keep)
    {
        if (keep >= this.Count)
        {
            return; // No need to trim if the series is already smaller than or equal to n
        }

        // Remove elements from the beginning of the list
        int elementsToRemove = this.Count - keep;
        RemoveRange(0, elementsToRemove);
    }
}
