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

    public TSeries Open { get { return _open; } }
    public TSeries High { get { return _high; } }
    public TSeries Low { get { return _low; } }
    public TSeries Close { get { return _close; } }
    public TSeries Volume { get { return _volume; } }
    public TSeries Hl2 { get { return _hl2; } }
    public TSeries Oc2 { get { return _oc2; } }
    public TSeries Ohl3 { get { return _ohl3; } }
    public TSeries Hlc3 { get { return _hlc3; } }
    public TSeries Ohlc4 { get { return _ohlc4; } }
    public TSeries Hlcc4 { get { return _hlcc4; } }

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
    public string SelectStr(int source)
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

    public void Add((DateTime t, double o, double h, double l, double c, double v) i, bool update = false) => 
        Add(i.t, i.o, i.h, i.l, i.c, i.v, update);
    
    public void Add(DateTime t, decimal o, decimal h, decimal l, decimal c, decimal v, bool update = false) => 
        Add(t, (double)o, (double)h, (double)l, (double)c, (double)v, update);
    
    public void Add(DateTime t, double o, double h, double l, double c, double v, bool update = false)
    {
        if (update)
        {
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
    }
}