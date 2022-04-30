namespace QuanTAlib;
using System;

/* <summary>
TBars class - includes all series for common data used in indicators and other calculations.
    Has a bit limited overloading and casting (compared to TSeries)
    Includes Select(int) method to simplify choosing the most optimal data source for indicators
    Includes the most basic pricing calcs: HL2, OC2, OHL3, HLC3, OHLC4, HLCC4 
        (it is 'cheaper' to calculate them once during data capture than each time during data analysis)

</summary> */

public class TBars : System.Collections.Generic.List<(DateTime t, double o, double h, double l, double c, double v)>
{
    private readonly TSeries _open = new();
    private readonly TSeries _high = new();
    private readonly TSeries _low = new();
    private readonly TSeries _close = new();
    private readonly TSeries _volume = new();
    private readonly TSeries _hl2 = new();
    private readonly TSeries _oc2 = new();
    private readonly TSeries _ohl3 = new();
    private readonly TSeries _hlc3 = new();
    private readonly TSeries _ohlc4 = new();
    private readonly TSeries _hlcc4 = new();

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

    public TSeries Select(int source)
    {
        return source switch
        {
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
    public static string SelectStr(int source)
    {
        return source switch
        {
            0 => "Open",
            1 => "High",
            2 => "Low",
            3 => "Close",
            4 => "HL2",
            5 => "OC2",
            6 => "OHL3",
            7 => "Typical",
            8 => "Mean",
            _ => "Weighted",
        };
    }

    public void
    Add((DateTime t, double o, double h, double l, double c, double v) i, bool update = false)
      => Add(i.t, i.o, i.h, i.l, i.c, i.v, update);

    public void Add(DateTime t, decimal o, decimal h, decimal l, decimal c, decimal v, bool update = false)
      => Add(t, (double)o, (double)h, (double)l, (double)c, (double)v, update);

    public void Add(DateTime t, double o, double h, double l, double c, double v, bool update = false)
    {
        if (update)
        {
            this[this.Count - 1] = (t, o, h, l, c, v);
            _open[_open.Count - 1] = (t, o);
            _high[_high.Count - 1] = (t, h);
            _low[_low.Count - 1] = (t, l);
            _close[_close.Count - 1] = (t, c);
            _volume[_volume.Count - 1] = (t, v);
            _hl2[_hl2.Count - 1] = (t, (h + l) * 0.5);
            _oc2[_oc2.Count - 1] = (t, (o + c) * 0.5);
            _ohl3[_ohl3.Count - 1] = (t, (o + h + l) * 0.333333333333333);
            _hlc3[_hlc3.Count - 1] = (t, (h + l + c) * 0.333333333333333);
            _ohlc4[_ohlc4.Count - 1] = (t, (o + h + l + c) * 0.25);
            _hlcc4[_hlcc4.Count - 1] = (t, (h + l + c + c) * 0.25);
        }
        else
        {
            base.Add((t, o, h, l, c, v));
            _open.Add((t, o));
            _high.Add((t, h));
            _low.Add((t, l));
            _close.Add((t, c));
            _volume.Add((t, v));
            _hl2.Add((t, (h + l) * 0.5));
            _oc2.Add((t, (o + c) * 0.5));
            _ohl3.Add((t, (o + h + l) * 0.333333333333333));
            _hlc3.Add((t, (h + l + c) * 0.333333333333333));
            _ohlc4.Add((t, (o + h + l + c) * 0.25));
            _hlcc4.Add((t, (h + l + c + c) * 0.25));
        }
        this.OnEvent(update);
    }

    // delegate used by event handler + event handler (Pub == publisher)
    public delegate
        void NewDataEventHandler(object source, TSeriesEventArgs args);
    public event NewDataEventHandler Pub;

    // Broadcast handler - only to valid targets
    protected virtual void OnEvent(bool update = false)
    {
        if (Pub != null && Pub.Target != this)
        {
            Pub(this, new TSeriesEventArgs { update = update });
        }
    }


}
