namespace QuanTAlib;
using System;

/* <summary>
ADD - adding TSeries+TSeries together, or TSeries+double, or double+TSeries

Remarks: 
    Most of scaffolding is packaged in abstracty class Pair_TSeries_Indicator.
    
</summary> */

public class ADD_Series : Pair_TSeries_Indicator 
{
    public ADD_Series(TSeries d1, TSeries d2 ) : base(d1, d2) {
        if (base._d1.Count > 0 && base._d2.Count > 0) { for (int i=0; i< base._d1.Count; i++) { this.Add(base._d1[i], base._d2[i], false); }  }
    }
    public ADD_Series(TSeries d1, double dd2 ) : base(d1, dd2) {
        if (base._d1.Count > 0) { for (int i=0; i< base._d1.Count; i++) { this.Add(base._d1[i], (base._d1[i].t, dd2), false); }  }
    }
    public ADD_Series(double dd1, TSeries d2 ) : base(dd1, d2) {
        if (base._d2.Count > 0) { for (int i=0; i< base._d2.Count; i++) { this.Add((base._d2[i].t, dd1), base._d2[i], false); }  }
    }

    public override void Add((System.DateTime t, double v)TValue1, (System.DateTime t, double v)TValue2, bool update) 
    {
        (System.DateTime t, double v) result = ((TValue1.t > TValue2.t) ? TValue1.t : TValue2.t, 
                TValue1.v+TValue2.v);
        if (update) { base[base.Count - 1] = result; } else { base.Add(result); }
    }
}
