namespace QuanTAlib;
using System;

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
		if (update)
		{
			this._bufferhi[this._bufferhi.Count - 1] = TBar.h;
			this._bufferlo[this._bufferlo.Count - 1] = TBar.l;
		}
		else
		{
			this._bufferhi.Add(TBar.h);
			this._bufferlo.Add(TBar.l);
		}
		if (this._bufferhi.Count > this._p && this._p != 0)
		{ this._bufferhi.RemoveAt(0); }
		if (this._bufferlo.Count > this._p && this._p != 0)
		{ this._bufferlo.RemoveAt(0); }

		double _max = TBar.h;
		double _min = TBar.l;
		for (int i = 0; i < this._bufferhi.Count; i++)
		{
			_max = Math.Max(this._bufferhi[i], _max);
			_min = Math.Min(this._bufferlo[i], _min);
		}
		double _mid = (_max + _min) * 0.5;

		var result = (TBar.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _mid);

		base.Add(result, update);
	}
}