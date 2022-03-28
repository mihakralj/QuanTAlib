namespace QuantLib;

using System;

public class TBars
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

    public TSeries open { get { return _open; } }
    public TSeries high { get { return _high; } }
    public TSeries low { get { return _low; } }
    public TSeries close { get { return _close; } }
    public TSeries volume { get { return _volume; } }
    public TSeries hl2 { get { return _hl2; } }
    public TSeries oc2 { get { return _oc2; } }
    public TSeries ohl3 { get { return _ohl3; } }
    public TSeries hlc3 { get { return _hlc3; } }
    public TSeries ohlc4 { get { return _ohlc4; } }
    public TSeries hlcc4 { get { return _hlcc4; } }

    public TSeries Select(int source) {
        switch (source)
        {
            case 0: return this._open;
            case 1: return this._high;
            case 2: return this._low;
            case 3: return this._close;
            case 4: return this._hl2;
            case 5: return this._oc2;
            case 6: return this._ohl3;
            case 7: return this._hlc3;
            case 8: return this._ohlc4;
            default: return this._hlcc4;
        }
    }
    public String SelectStr(int source)
    {
        switch (source)
        {
            case 0: return "Open";
            case 1: return "High";
            case 2: return "Low";
            case 3: return "Close";
            case 4: return "HL2";
            case 5: return "OC2";
            case 6: return "OHL3";
            case 7: return "HLC3";
            case 8: return "OHLC4";
            default: return "Weighted";
        }
    }

    public void Add((DateTime t, double o, double h, double l, double c, double v) i, bool update = false) => this.Add(i.t, i.o, i.h, i.l, i.c, i.v, update);
    public void Add(DateTime t, decimal o, decimal h, decimal l, decimal c, decimal v, bool update = false) => this.Add(t, (double)o, (double)h, (double)l, (double)c, (double)v, update);
    public void Add(DateTime t, double o, double h, double l, double c, double v, bool update = false)
    {
        if (update)
        {
            this._open[this._open.Count - 1] = (t, o);
            this._high[this._high.Count - 1] = (t, h);
            this._low[this._low.Count - 1] = (t, l);
            this._close[this._close.Count - 1] = (t, c);
            this._volume[this._volume.Count - 1] = (t, v);
            this._hl2[this._hl2.Count - 1] = (t, (h + l) * 0.5);
            this._oc2[this._oc2.Count - 1] = (t, (o + c) * 0.5);
            this._ohl3[this._ohl3.Count - 1] = (t, (o + h + l) * 0.333333333333333);
            this._hlc3[this._hlc3.Count - 1] = (t, (h + l + c) * 0.333333333333333);
            this._ohlc4[this._ohlc4.Count - 1] = (t, (o + h + l + c) * 0.25);
            this._hlcc4[this._hlcc4.Count - 1] = (t, (h + l + c + c) * 0.25);
        }
        else
        {
            this._open.Add((t, o));
            this._high.Add((t, h));
            this._low.Add((t, l));
            this._close.Add((t, c));
            this._volume.Add((t, v));
            this._hl2.Add((t, (h + l) * 0.5));
            this._oc2.Add((t, (o + c) * 0.5));
            this._ohl3.Add((t, (o + h + l) * 0.333333333333333));
            this._hlc3.Add((t, (h + l + c) * 0.333333333333333));
            this._ohlc4.Add((t, (o + h + l + c) * 0.25));
            this._hlcc4.Add((t, (h + l + c + c) * 0.25));
        }
    }
}