// ADD - adding TSeries+TSeries together, or TSeries+double, or double+TSeries
using System;
namespace QuantLib;

public class ADD_Series : TSeries {
    readonly TSeries _d1;
    readonly TSeries _d2;
    readonly double _dd;

    public ADD_Series(TSeries d1, TSeries d2) {
        this._d1 = d1;
        this._d2 = d2;
        this._dd = 0;
        d1.Pub += this.Sub;
        d2.Pub += this.Sub;
        if (d1.Count > 0 && d2.Count > 0) 
            for (int i = 0; i < Math.Min(d1.Count,d2.Count); i++) this.Add(d1[i], d2[i], false);
    }

    public ADD_Series(TSeries d1, double dd) {
        this._d1 = d1;
        this._d2 = null;
        this._dd = dd;
        d1.Pub += this.Sub;
        if (d1.Count > 0) for (int i = 0; i < d1.Count; i++) this.Add(d1[i], dd, false);
    }

    public ADD_Series(double dd, TSeries d1){
        this._d1 = d1;
        this._d2 = null;
        this._dd = dd;
        d1.Pub += this.Sub;
        if (d1.Count > 0) for (int i = 0; i < d1.Count; i++) this.Add(d1[i], dd, false);
    }

    public void Add((System.DateTime t, double v) d1, (System.DateTime t, double v) d2, bool update = false){
        (System.DateTime t, double v) result = ((d1.t>d2.t)?d1.t:d2.t, d1.v+d2.v);
        if (update) base[base.Count-1] = result; else base.Add(result);
    }

    public void Add((System.DateTime t, double v) d1, double dd, bool update = false){
        (System.DateTime t, double v) result = (d1.t, d1.v+dd);
        if (update) base[base.Count-1] = result; else base.Add(result);
    }

    public void Add(bool update = false) { 
        if (this._d2 ==null) 
            this.Add(this._d1[this._d1.Count-1], this._dd, update);
        else if (this._d1.Count>0 && this._d2.Count>0 && 
                 this._d1[this._d1.Count-1].t == this._d2[this._d2.Count-1].t &&
                 this[this.Count-1].t != this._d1[this._d1.Count-1].t) 
            this.Add(this._d1[this._d1.Count-1], this._d2[this._d2.Count-1], update); 
    }

    public new void Sub(object source, TSeriesEventArgs e) => this.Add(e.update);
}