namespace QuantLib;

using System;

public class BAR_Series : TSeries 
{
    private readonly TSeries _o;
    private readonly TSeries _h;
    private readonly TSeries _l;
    private readonly TSeries _c;
    private readonly TSeries _v;
    private readonly Type _type;
    
    public enum Type {open = 0, high = 1, low = 2, close = 3, volume = 4}

    public TSeries open { get { return _o; } }
    public TSeries high { get { return _h; } }
    public TSeries low { get { return _l; } }
    public TSeries close { get { return _c; } }
    public TSeries volume { get { return _v; } }

    public BAR_Series (Type t = Type.close) {
        _type = t;
        _o = new();
        _h = new();
        _l = new();
        _c = new();
        _v = new();
        // this should point to _ohlc...
    }

    public void Add((DateTime t, double o, double h, double l, double c, double v) i, bool update = false) => this.Add(i.t, i.o, i.h, i.l, i.c, i.v, update);
    public void Add(DateTime t, decimal o, decimal h, decimal l, decimal c, decimal v, bool update = false) => this.Add(t, (double)o, (double)h, (double)l, (double)c, (double)v, update);
    public void Add(DateTime t, double o, double h, double l, double c, double v, bool update = false)
    {
        if (update)
        {
            this._o[this._o.Count - 1] = (t, o);
            this._h[this._h.Count - 1] = (t, h);
            this._l[this._l.Count - 1] = (t, l);
            this._c[this._c.Count - 1] = (t, c);
            this._v[this._v.Count - 1] = (t, v);
            switch (_type) {
                case Type.open: this[this.Count-1] = (t, o); break;
                case Type.high: this[this.Count-1] = (t, h); break;
                case Type.low: this[this.Count-1] = (t, l); break;
                case Type.close: this[this.Count-1] = (t, c); break;
                case Type.volume: this[this.Count-1] = (t, v); break;
            }
        }
        else
        {
            this._o.Add((t, o));
            this._h.Add((t, h));
            this._l.Add((t, l));
            this._c.Add((t, c));
            this._v.Add((t, v));
            switch (_type) {
                case Type.open: this.Add((t, o)); break;
                case Type.high: this.Add((t, h)); break;
                case Type.low: this.Add((t, l)); break;
                case Type.close: this.Add((t, c)); break;
                case Type.volume: this.Add((t, v)); break;
            }
        }
    }
}