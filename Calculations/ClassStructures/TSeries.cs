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


public class TSeriesEventArgs : EventArgs{
  public bool update { get; set; }
}

public class TSeries : List<(DateTime t, double v)> {

	public static implicit operator (DateTime t, double v)(TSeries l) => l[^1];
	public static implicit operator double(TSeries l) => l[^1].v;
	public static implicit operator DateTime(TSeries l) => l[^1].t;
	public List<DateTime> t => this.Select(item => item.t).ToList();
	public List<double> v => this.Select(item => item.v).ToList();
	public int Length => this.Count;

	public TSeries Tail(int count = 10) {
		var tailSeries = new TSeries();
		tailSeries.AddRange(this.Skip(Math.Max(0, this.Count - count)).Take(count));
		return tailSeries;
	}
	public (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (update) { this[^1] = TValue; }
		else { base.Add(TValue); }
		OnEvent(update);
		return TValue;
	}

	public void Add(DateTime t, double v, bool update = false) => this.Add((t, v), update);
	public void Add(double v, bool update = false) => this.Add((DateTime.Now, v), update);
	protected virtual void OnEvent(bool update = false) {
		Pub?.Invoke(this, new TSeriesEventArgs { update = update });
	}

	public delegate void NewDataEventHandler(object source, TSeriesEventArgs args);
	public event NewDataEventHandler Pub;

	public void Sub(object source, TSeriesEventArgs e) {
		TSeries ss = (TSeries)source;
		if (ss.Count > 0) {
			this.AddRange(ss);
		}
		else {
			this.Add(ss[^1], e.update);
		}
	}
}
