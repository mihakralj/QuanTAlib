namespace QuanTAlib;
using System;

/* <summary>
TBars class - includes all series for common data used in indicators and other calculations.
    Has a bit limited overloading and casting (compared to TSeries)
    Includes Select(int) method to simplify choosing the most optimal data source for indicators
    Includes the most basic pricing calcs: HL2, OC2, OHL3, HLC3, OHLC4, HLCC4
        (it is 'cheaper' to calculate them once during data capture than each time during data analysis)

</summary> */

public class TBars : System.Collections.Generic.List<(DateTime t, double o, double h, double l, double c, double v)> {
    public string Name { get; set; }
    private readonly TSeries _open = new("open");
    private readonly TSeries _high = new("high");
    private readonly TSeries _low = new("low");
    private readonly TSeries _close = new("close");
    private readonly TSeries _volume = new("volume");
    private readonly TSeries _hl2 = new("HL2");
    private readonly TSeries _oc2 = new("OC2");
    private readonly TSeries _ohl3 = new("OHL3");
    private readonly TSeries _hlc3 = new("HLC3");
    private readonly TSeries _ohlc4 = new("OHLC4");
    private readonly TSeries _hlcc4 = new("HLCC4");

    public TSeries Open => this._open;
    public TSeries High => this._high;
    public TSeries Low => this._low;
    public TSeries Close => this._close;
    public TSeries Volume => this._volume;
    public TSeries HL2 => this._hl2;
    public TSeries OC2 => this._oc2;
    public TSeries OHL3 => this._ohl3;
    public TSeries HLC3 => this._hlc3;
    public TSeries OHLC4 => this._ohlc4;
    public TSeries HLCC4 => this._hlcc4;

    public TBars() { }

    public TBars(string Name) {
        this.Name = Name;
    }

    public (DateTime t, double o, double h, double l, double c, double v) Last => this[^1];
    public TBars Tail(int count = 10) {
        TBars outBars = new();
        if (count > this.Count) { count = this.Count; }
        for (int i = this.Count - count; i < this.Count; i++) { outBars.Add(this[i]); }
        return outBars;
    }
    public TSeries Select(int source) {
        return source switch {
            0 => _open,
            1 => _high,
            2 => _low,
            3 => _close,
            4 => _hl2,
            5 => _oc2,
            6 => _ohl3,
            7 => _hlc3,
            8 => _ohlc4,
            _ => _hlcc4,
        };
    }
    public static string SelectStr(int source) {
        return source switch {
            0 => "Open",
            1 => "High",
            2 => "Low",
            3 => "Close",
            4 => "HL2",
            5 => "OC2",
            6 => "OHL3",
            7 => "HLC3",
            8 => "OHLC4",
            _ => "HLCC4",
        };
    }

    public virtual (DateTime t, double v) Add((double o, double h, double l, double c, double v) p, bool update = false) =>
        Add((t: (this.Count == 0) ? DateTime.Today : this[^1].t.AddDays(1), p.o, p.h, p.l, p.c, p.v), update);

    public virtual (DateTime t, double v) Add(double o, double h, double l, double c, double v, bool update = false) =>
        Add((o, h, l, c, v), update);

    public virtual (DateTime t, double v) Add(DateTime t, double o, double h, double l, double c, double v, bool update = false) =>
        this.Add((t, o, h, l, c, v), update);

    public virtual (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false) {
        if (update) { this[^1] = TBar; } else { base.Add(TBar); }

        _open.Add((TBar.t, TBar.o), update);
        _high.Add((TBar.t, TBar.h), update);
        _low.Add((TBar.t, TBar.l), update);
        _close.Add((TBar.t, TBar.c), update);
        _volume.Add((TBar.t, TBar.v), update);
        _hl2.Add((TBar.t, (TBar.h + TBar.l) * 0.5), update);
        _oc2.Add((TBar.t, (TBar.o + TBar.c) * 0.5), update);
        _ohl3.Add((TBar.t, (TBar.o + TBar.h + TBar.l) * 0.333333333333333), update);
        _hlc3.Add((TBar.t, (TBar.h + TBar.l + TBar.c) * 0.333333333333333), update);
        _ohlc4.Add((TBar.t, (TBar.o + TBar.h + TBar.l + TBar.c) * 0.25), update);
        _hlcc4.Add((TBar.t, (TBar.h + TBar.l + TBar.c + TBar.c) * 0.25), update);

        this.OnEvent(update);
        return (TBar.t, (TBar.o + TBar.h + TBar.l + TBar.c) * 0.25);
    }

    public delegate void NewDataEventHandler(object source, TSeriesEventArgs args);
    public event NewDataEventHandler Pub;
    protected virtual void OnEvent(bool update = false) {
        if (Pub != null && Pub.Target != this) {
            Pub(this, new TSeriesEventArgs { update = update });
        }
    }

    public void Sub(object source, TSeriesEventArgs e) {
        TBars ss = (TBars)source; if (ss.Count > 1) {
            for (int i = 0; i < ss.Count; i++) { this.Add(ss[i]); }
        } else {
            this.Add(ss[^1], e.update);
        }
    }

    /// common helpers
    public static void BufferTrim(System.Collections.Generic.List<double> buffer, double value, int period, bool update) {
        if (!update) {
            buffer.Add(value);
            if (buffer.Count > period && period > 0) { buffer.RemoveAt(0); }
            return;
        }
        buffer[^1] = value;
    }
    public virtual void Reset() {
    }
}
