namespace QuanTAlib;
using System;

/* <summary>
COMPARE - Generates +1 if A is above B, -1 if A is below B and 0 if A=B

   
</summary> */

public class COMPARE_Series : Pair_TSeries_Indicator {

	public COMPARE_Series(TSeries d1, TSeries d2) : base(d1, d2) {
		if (base._d1.Count > 0 && base._d2.Count > 0) { for (int i = 0; i < base._d1.Count; i++) { this.Add(base._d1[i], base._d2[i], false); } }
	}
	public COMPARE_Series(TSeries d1, double dd2) : base(d1, dd2) {
		if (base._d1.Count > 0) { for (int i = 0; i < base._d1.Count; i++) { this.Add(base._d1[i], (base._d1[i].t, dd2), false); } }
	}
	public COMPARE_Series(double dd1, TSeries d2) : base(dd1, d2) {
		if (base._d2.Count > 0) { for (int i = 0; i < base._d2.Count; i++) { this.Add((base._d2[i].t, dd1), base._d2[i], false); } }
	}

	public override void Add((System.DateTime t, double v) TValue1, (System.DateTime t, double v) TValue2, bool update) {

		double val = TValue1.v > TValue2.v ? 1 : -1;
		val = TValue1.v == TValue2.v ? 0 : val;
		(System.DateTime t, double v) over = ((TValue1.t > TValue2.t) ? TValue1.t : TValue2.t, TValue1.v > TValue2.v ? 1 : val);
		if (update) { base[^1] = over; }
		else { base.Add(over); }


	}
}


