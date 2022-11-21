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
		_lastadl = _lastlastadl = 0;
		if (_bars.Count > 0) { base.Add(_bars); }
	}

	public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update)
	{
		if (update) { this._lastadl = this._lastlastadl; }

		double _adl = 0;
		double tmp = TBar.h - TBar.l;
		if (tmp > 0.0 ) { _adl = _lastadl + ((2*TBar.c - TBar.l - TBar.h) / tmp * TBar.v); }

		this._lastlastadl = this._lastadl;
		this._lastadl = _adl;

        base.Add((TBar.t, _adl), update, _NaN);
    }
}