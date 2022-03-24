namespace QuantLib;

public class TSeries : System.Collections.Generic.List<(System.DateTime t, double v)>
{
    public static implicit operator (System.DateTime t, double v)(TSeries l) => l[l.Count-1];
    public static implicit operator double(TSeries l) => l[l.Count - 1].v;

    public void Add(System.DateTime t, double v, bool update = false) {
        this.Add((t, v), update);
    }
    public void Add((System.DateTime t, double v) TValue, bool update = false) {
        if (update) this[this.Count - 1] = TValue; else base.Add(TValue);
    }
}  


public class TBars
{
    public TSeries open = new();
    public TSeries high = new();
    public TSeries low = new();
    public TSeries close = new();
    public TSeries volume = new();
    public TSeries hl2 = new();
    public TSeries oc2 = new();
    public TSeries ohl3 = new();
    public TSeries hlc3 = new();
    public TSeries ohlc4 = new();
    public TSeries hlcc4 = new();
    public void Add((System.DateTime t, double o, double h, double l, double c, double v) i, bool update = false)
    {
        this.Add(i.t, i.o, i.h, i.l, i.c, i.v, update);
    }
    public void Add(System.DateTime t, double o, double h, double l, double c, double v, bool update = false)
    {
        if (update)
        {
            this.open[this.open.Count - 1] = (t, o);
            this.high[this.high.Count - 1] = (t, h);
            this.low[this.low.Count - 1] = (t, l);
            this.close[this.close.Count - 1] = (t, c);
            this.volume[this.volume.Count - 1] = (t, v);
            this.hl2[this.hl2.Count - 1] = (t, (h + l) * 0.5);
            this.oc2[this.oc2.Count - 1] = (t, (o + c) * 0.5);
            this.ohl3[this.ohl3.Count - 1] = (t, (o + h + l) * 0.333333333333333);
            this.hlc3[this.hlc3.Count - 1] = (t, (h + l + c) * 0.333333333333333);
            this.ohlc4[this.ohlc4.Count - 1] = (t, (o + h + l + c) * 0.25);
            this.hlcc4[this.hlcc4.Count - 1] = (t, (h + l + c + c) * 0.25);
        }
        else
        {
            this.open.Add((t, o));
            this.high.Add((t, h));
            this.low.Add((t, l));
            this.close.Add((t, c));
            this.volume.Add((t, v));
            this.hl2.Add((t, (h + l) * 0.5));
            this.oc2.Add((t, (o + c) * 0.5));
            this.ohl3.Add((t, (o + h + l) * 0.333333333333333));
            this.hlc3.Add((t, (h + l + c) * 0.333333333333333));
            this.ohlc4.Add((t, (o + h + l + c) * 0.25));
            this.hlcc4.Add((t, (h + l + c + c) * 0.25));
        }
    }
}