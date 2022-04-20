namespace QuanTAlib;
using System;

/* <summary>
TSeries is the cornerstone of all QuanTAlib classess. 
    TSeries is a single List of tuples (time, value) and contains several operators, casts, overloads 
    and other helpers that simplify usage of library. 
    Think of TSeries as an equivalent of Numpy array.
    
        - includes Length property (to mimic array's method)
        - includes publishing and subscribing methods that attach to events

</summary> */
public class TSeries : System.Collections.Generic.List<(DateTime t, double v)>
{
    // when asked for a (t,v) tuple, return the last (t,v) on the List
    public static implicit operator (DateTime t, double v)(TSeries l) => l[l.Count - 1];

    // when asked for a (double), return the value part of the last tuple on the list
    public static implicit operator double(TSeries l) => l[l.Count - 1].v;

    // when asked for a (DateTime), return the DateTime part of the last tuple on the list
    public static implicit operator DateTime(TSeries l) => l[l.Count - 1].t;
    
    //convert from tuple List(t,v) to single List(DateTime)
    public System.Collections.Generic.List<DateTime> t { 
        get { System.Collections.Generic.List<DateTime> TList = new();
            for (int i = 0; i < this.Count; i++) { TList.Add(this[i].t); }
            return TList;
        }
    }

    //convert from tuple List(t,v) to single List(double)
    public System.Collections.Generic.List<double> v { 
        get { System.Collections.Generic.List<double> VList = new();
            for (int i = 0; i < this.Count; i++) { VList.Add(this[i].v); }
            return VList;
        }
    }

    public int Length => this.Count;

    // add/update one (t,v) tuple to/at the end of the list
    public void Add((DateTime t, double v) TValue, bool update = false)
    {
        if (update) { this[this.Count - 1] = TValue; }
        else { base.Add(TValue); }
        this.OnEvent(update);
    }

    public void Add(DateTime t, double v, bool update = false) => this.Add((t, v), update);

    public void Add(double v, bool update = false) => this.Add((DateTime.Now, v), update);

    // Broadcast handler - only to valid targets
    protected virtual void OnEvent(bool update = false)
    {
        if (Pub != null && Pub.Target != this)
        {
            Pub(this, new TSeriesEventArgs { update = update });
        }
    }

    // delegate used by event handler + event handler (Pub == publisher)
    public delegate
        void NewDataEventHandler(object source, TSeriesEventArgs args);
    public event NewDataEventHandler Pub;

    public void Sub(object source, TSeriesEventArgs e)
    {
        TSeries ss = (TSeries)source;
        if (ss.Count > 0)
        {
            for (int i = 0; i < ss.Count; i++)
            {
                this.Add(ss[i]);
            }
        }
        else
        {
            this.Add(ss[ss.Count - 1], e.update);
        }
    }
}

//  EventArgs extension - carries the update field
public class TSeriesEventArgs : EventArgs
{
    public bool update { get; set; }
}
