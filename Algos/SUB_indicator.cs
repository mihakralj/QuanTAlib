// SUB - subtracting TSeries-TSeries, or TSeries-double, or double-TSeries
using System;
namespace QuantLib;

public class SUB_Series : TSeries {
    TSeries _d1, _d2;
    double _dd;
    byte _type;

    public SUB_Series(TSeries d1, TSeries d2) {
        _d1 = d1;
        _d2 = d2;
        _dd = 0;
        _type = 1;
        d1.Pub += this.Sub;
        d2.Pub += this.Sub;
    }

    public SUB_Series(TSeries d1, double dd) {
        _d1 = d1;
        _d2 = d1;
        _dd = dd;
        _type = 2;
        d1.Pub += this.Sub;
    }

    public SUB_Series(double dd, TSeries d1){
        _d1 = d1;
        _d2 = d1;
        _dd = dd;
        _type = 3;
        d1.Pub += this.Sub;
    }

    public void Add((System.DateTime t, double v) d1, (System.DateTime t, double v) d2, bool update = false){
        (System.DateTime t, double v) result = ((d1.t>d2.t)?d1.t:d2.t, d1.v-d2.v);
        if (update) base[base.Count-1] = result; else base.Add(result);
    }

    public void Add((System.DateTime t, double v) d1, double dd, bool update = false){
        (System.DateTime t, double v) result = (d1.t, d1.v-dd);
        if (update) base[base.Count-1] = result; else base.Add(result);
    }

    public void Add(double dd, (System.DateTime t, double v) d1, bool update = false){
        (System.DateTime t, double v) result = (d1.t, dd-d1.v);
        if (update) base[base.Count-1] = result; else base.Add(result);
    }

    public void Add(bool update = false) { 
        if (_type==1 && _d1.Count>0 && _d2.Count>0 && _d1[_d1.Count-1].t == _d2[_d2.Count-1].t) 
            this.Add(this._d1[_d1.Count-1], this._d2[_d2.Count-1], update);
        else if (_type==2) 
            this.Add(this._d1[_d1.Count-1], this._dd, update);
        else if (_type==3) 
            this.Add(this._dd, this._d1[_d1.Count-1], update);
    }

    public void Sub(object source, TSeriesEventArgs e) { this.Add(e.update); }
}