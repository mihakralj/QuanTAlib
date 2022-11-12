namespace QuanTAlib;
using System;

/* <summary>
ADL: Chaikin Accumulation/Distribution Line
    ADL is a volume-based indicator that measures the cumulative Money Flow Volume:

    1. Money Flow Multiplier = [(Close  -  Low) - (High - Close)] /(High - Low) 
    2. Money Flow Volume = Money Flow Multiplier x Volume for the Period
    3. ADL = Previous ADL + Current Period's Money Flow Volume

Sources:
    https://school.stockcharts.com/doku.php?id=technical_indicators:accumulation_distribution_line

</summary> */

public class ADL_Series : Single_TBars_Indicator
{
	private double _lastadl, _lastlastadl;

	public ADL_Series(TBars source, bool useNaN = false) : base(source, 0, useNaN)
	{
		this._lastadl = this._lastlastadl = 0;
		if (_bars.Count > 0)
		{ base.Add(_bars); }
	}

	public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update)
	{
		if (update)
		{ this._lastadl = this._lastlastadl; }

		double _mfm = ((TBar.c - TBar.l) - (TBar.h - TBar.c)) / (TBar.h - TBar.l);
		double _mfv = _mfm * TBar.v;
		double _adl = this._lastadl + _mfv;

		this._lastlastadl = this._lastadl;
		this._lastadl = _adl;

		var ret = (TBar.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _adl);
		base.Add(ret, update);
	}
}