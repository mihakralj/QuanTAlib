namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
MIDPRICE: Midpoint price (highhest high + lowest low)/2 in the given period in the series.
    If period = 0 => period = full length of the series

</summary> */

public class MIDPRICE_Series : Single_TBars_Indicator
{
	public MIDPRICE_Series(TBars source, int period, bool useNaN = false) : base(source, period, useNaN)
	{
		if (base._bars.Count > 0)
		{ base.Add(base._bars); }
	}
	private readonly System.Collections.Generic.List<double> _bufferhi = new();
	private readonly System.Collections.Generic.List<double> _bufferlo = new();

	public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update)
	{
		Add_Replace_Trim(_bufferhi, TBar.h, _p, update);
        Add_Replace_Trim(_bufferlo, TBar.l, _p, update);

		double _max = _bufferhi.Max();
		double _min = _bufferlo.Min();
		double _mid = (_max + _min) * 0.5;

		base.Add((TBar.t, _mid), update, _NaN);
	}
}