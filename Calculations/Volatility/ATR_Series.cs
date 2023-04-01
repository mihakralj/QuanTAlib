namespace QuanTAlib;
using System;

/* <summary>
ATR: wildeR Moving Average
    The average true range (ATR) is a price volatility indicator
    showing the average price variation of assets within a given time period.

Sources:
    https://en.wikipedia.org/wiki/Average_true_range
    https://www.tradingview.com/wiki/Average_True_Range_(ATR)
    https://www.investopedia.com/terms/a/atr.asp

</summary> */

public class ATR_Series : Single_TBars_Indicator {
	private readonly System.Collections.Generic.List<double> _buffer = new();
	private readonly double _k;
	private double _lastatr, _lastlastatr, _cm1, _lastcm1, _sum, _oldsum;
	private readonly int _period;

	public ATR_Series(TBars source, int period, bool useNaN = false) : base(source, period, useNaN) {
		_period = period;
		_k = 1.0 / (double)(_p);
		_lastatr = _lastlastatr = _cm1 = _lastcm1 = _sum = _oldsum = 0;
		if (this._bars.Count > 0) { base.Add(this._bars); }
	}

	public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update) {
		if (update) { _lastatr = _lastlastatr; _cm1 = _lastcm1; _sum = _oldsum; }
		else { _lastlastatr = _lastatr; _lastcm1 = _cm1; _oldsum = _sum; }

		if (this.Count == 0) { _cm1 = TBar.c; }
		double d1 = Math.Abs(TBar.h - TBar.l);
		double d2 = Math.Abs(_cm1 - TBar.h);
		double d3 = Math.Abs(_cm1 - TBar.l);
		(DateTime t, double v) d = (TBar.t, Math.Max(d1, Math.Max(d2, d3)));
		_cm1 = TBar.c;

		double _atr = 0;
		if (this.Count == 0) { _atr = d.v; }
		else if (this.Count < _p + 1) { _sum += d.v; _atr = _sum / (this.Count); }
		else { _atr = _k * (d.v - _lastatr) + _lastatr; }
		_lastatr = _atr;

		var ret = (d.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _atr);
		base.Add(ret, update);
	}
}