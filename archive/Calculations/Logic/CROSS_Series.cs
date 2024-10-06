namespace QuanTAlib;
using System;

/* <summary>
OVER - Generates +1 if A is above B, -1 if A is below B and 0 if A=B

Remarks: 
    OVER.Cross generates 1 when A breaks B from below and -1 when A breaks B from above
    
</summary> */

public class CROSS_Series : Pair_TSeries_Indicator {
    public TSeries Cross { get; set; } = new();

    private double _previous = double.NaN;
    public CROSS_Series(TSeries d1, TSeries d2) : base(d1, d2) {
        if (base._d1.Count > 0 && base._d2.Count > 0) { for (int i = 0; i < base._d1.Count; i++) { this.Add(base._d1[i], base._d2[i], false); } }
    }
    public CROSS_Series(TSeries d1, double dd2) : base(d1, dd2) {
        if (base._d1.Count > 0) { for (int i = 0; i < base._d1.Count; i++) { this.Add(base._d1[i], (base._d1[i].t, dd2), false); } }
    }
    public CROSS_Series(double dd1, TSeries d2) : base(dd1, d2) {
        if (base._d2.Count > 0) { for (int i = 0; i < base._d2.Count; i++) { this.Add((base._d2[i].t, dd1), base._d2[i], false); } }
    }

    public override void Add((System.DateTime t, double v) TValue1, (System.DateTime t, double v) TValue2, bool update) {

        double val = TValue1.v > TValue2.v ? 1 : -1;
        val = TValue1.v == TValue2.v ? 0 : val;
        double over = TValue1.v > TValue2.v ? 1 : val;

        val = (_previous < over) ? 1 : -1;
        val = ((_previous == over) || Double.IsNaN(this._previous) || (this._previous == 0)) ? 0 : val;
        (System.DateTime t, double v) result = ((TValue1.t > TValue2.t) ? TValue1.t : TValue2.t, val);

        this._previous = over;

        if (update) { base[^1] = result; } else { base.Add(result); }

    }
}


