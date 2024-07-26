namespace QuanTAlib;

using System;
using System.Linq;

/* <summary>
MAMA: MESA Adaptive Moving Average
    Created by John Ehlers, the MAMA indicator is a 5-period adaptive moving average of
    high/low price that uses classic electrical radio-frequency signal processing algorithms
    to reduce noise.

    KAMAi = KAMAi - 1 + SC * ( price - KAMAi-1 )

Sources:
    https://mesasoftware.com/papers/MAMA.pdf
    https://www.tradingview.com/script/foQxLbU3-Ehlers-MESA-Adaptive-Moving-Average-LazyBear/

</summary> */

public class MAMA_Series : TSeries {
	private int _len;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	private double sumPr;
	private double fastl, slowl;
	private (double i, double i1, double i2, double i3, double i4, double i5, double i6, double io) pr, i1, q1, sm, dt;
	private (double i, double i1, double io) i2, q2, re, im, pd, ph, mama, fama;
	public TSeries Fama { get; }
	private double mamaseed, famaseed;

	//core constructors

	public MAMA_Series(double fastlimit, double slowlimit, bool useNaN) {
		_period = (int)(2 / fastlimit) - 1;
		fastl = fastlimit;
		slowl = slowlimit;
		Fama = new TSeries();
		_NaN = useNaN;
		Name = $"MAMA({_period})";
		_len = 0;
	}
	public MAMA_Series(TSeries source, double fastlimit, double slowlimit, bool useNaN = false) : this(fastlimit, slowlimit, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public MAMA_Series() : this(period: 0, useNaN: false) { }
	public MAMA_Series(int period) : this(period, useNaN: false) { }
	public MAMA_Series(int period, bool useNaN) : this(fastlimit: 2 / (period + 1), slowlimit: 0.2 / (period + 1), useNaN) {
		_period = period;
	}
	public MAMA_Series(TBars source) : this(source.Close, period: 0, useNaN: false) { }
	public MAMA_Series(TBars source, int period) : this(source.Close, period, false) { }
	public MAMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public MAMA_Series(TSeries source, int period) : this(source, period, false) { }
	public MAMA_Series(TSeries source, int period, bool useNaN) : this(source, fastlimit: 2 / ((double)period + 1), slowlimit: 0.2 / ((double)period + 1), useNaN: useNaN) { }


	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}
		if (!update) {
			// roll forward (oldx = x)
			pr.io = pr.i6; pr.i6 = pr.i5; pr.i5 = pr.i4; pr.i4 = pr.i3; pr.i3 = pr.i2; pr.i2 = pr.i1; pr.i1 = pr.i;
			i1.io = i1.i6; i1.i6 = i1.i5; i1.i5 = i1.i4; i1.i4 = i1.i3; i1.i3 = i1.i2; i1.i2 = i1.i1; i1.i1 = i1.i;
			q1.io = q1.i6; q1.i6 = q1.i5; q1.i5 = q1.i4; q1.i4 = q1.i3; q1.i3 = q1.i2; q1.i2 = q1.i1; q1.i1 = q1.i;
			dt.io = dt.i6; dt.i6 = dt.i5; dt.i5 = dt.i4; dt.i4 = dt.i3; dt.i3 = dt.i2; dt.i2 = dt.i1; dt.i1 = dt.i;
			sm.io = sm.i6; sm.i6 = sm.i5; sm.i5 = sm.i4; sm.i4 = sm.i3; sm.i3 = sm.i2; sm.i2 = sm.i1; sm.i1 = sm.i;
			i2.io = i2.i1; i2.i1 = i2.i; q2.io = q2.i1; q2.i1 = q2.i;
			re.io = re.i1; re.i1 = re.i; im.io = im.i1; im.i1 = im.i;
			pd.io = pd.i1; pd.i1 = pd.i; ph.io = ph.i1; ph.i1 = ph.i;
			mama.io = mama.i1; mama.i1 = mama.i;
			fama.io = fama.i1;
			fama.i1 = fama.i;
			_len++;
		}
		if (_period == 0) {
			fastl = 2 / (double)_len;
			slowl = fastl * 0.1;
		}
		if (_period == 1) {
			fastl = 1;
			slowl = 1;
		}
		var i = _len - 1;
		pr.i = TValue.v;
		if (i > 5) {
			var adj = 0.075 * pd.i1 + 0.54;

			// smooth and detrender
			sm.i = (4 * pr.i + 3 * pr.i1 + 2 * pr.i2 + pr.i3) / 10;
			dt.i = (0.0962 * sm.i + 0.5769 * sm.i2 - 0.5769 * sm.i4 - 0.0962 * sm.i6) * adj;

			// in-phase and quadrature
			q1.i = (0.0962 * dt.i + 0.5769 * dt.i2 - 0.5769 * dt.i4 - 0.0962 * dt.i6) * adj;
			i1.i = dt.i3;

			// advance the phases by 90 degrees
			double jI = (0.0962 * i1.i + 0.5769 * i1.i2 - 0.5769 * i1.i4 - 0.0962 * i1.i6) * adj;
			double jQ = (0.0962 * q1.i + 0.5769 * q1.i2 - 0.5769 * q1.i4 - 0.0962 * q1.i6) * adj;

			// phasor addition for 3-bar averaging
			i2.i = i1.i - jQ;
			q2.i = q1.i + jI;

			i2.i = 0.2 * i2.i + 0.8 * i2.i1; // smoothing it
			q2.i = 0.2 * q2.i + 0.8 * q2.i1;

			// homodyne discriminator
			re.i = i2.i * i2.i1 + q2.i * q2.i1;
			im.i = i2.i * q2.i1 - q2.i * i2.i1;

			re.i = 0.2 * re.i + 0.8 * re.i1; // smoothing it
			im.i = 0.2 * im.i + 0.8 * im.i1;

			// calculate period
			pd.i = im.i != 0 && re.i != 0 ? 6.283185307179586 / Math.Atan(im.i / re.i) : 0d;

			// adjust period to thresholds
			pd.i = pd.i > 1.5 * pd.i1 ? 1.5 * pd.i1 : pd.i;
			pd.i = pd.i < 0.67 * pd.i1 ? 0.67 * pd.i1 : pd.i;
			pd.i = pd.i < 6d ? 6d : pd.i;
			pd.i = pd.i > 50d ? 50d : pd.i;

			// smooth the period
			pd.i = 0.2 * pd.i + 0.8 * pd.i1;

			// determine phase position
			ph.i = i1.i != 0 ? Math.Atan(q1.i / i1.i) * 57.29577951308232 : 0;

			// change in phase
			var delta = Math.Max(ph.i1 - ph.i, 1d);

			// adaptive alpha value
			var alpha = Math.Max(fastl / delta, slowl);

			// final indicators
			mama.i = alpha * (pr.i - mama.i1) + mama.i1;
			fama.i = 0.5d * alpha * (mama.i - fama.i1) + fama.i1;
		}
		else {
			sumPr += pr.i;
			pd.i = sm.i = dt.i = i1.i = q1.i = i2.i = q2.i = re.i = im.i = ph.i = 0;
			mama.i = fama.i = sumPr / (i + 1);

			if (_len == 1) {
				mamaseed = famaseed = TValue.v;
			}
			else {
				mamaseed = fastl * (TValue.v - mamaseed) + mamaseed;
				famaseed = slowl * (TValue.v - famaseed) + famaseed;
			}
		}

		double _fama = (i > 5) ? fama.i : famaseed;
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _fama);
		Fama.Add(res, update);
		double _mama = (i > 5) ? mama.i : mamaseed;
		res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _mama);
		return base.Add(res, update);
	}

	//variation of Add()
	public override (DateTime t, double v) Add(TSeries data) {
		if (data == null) { return (DateTime.Today, Double.NaN); }
		foreach (var item in data) { Add(item, false); }
		return _data.Last;
	}
	public (DateTime t, double v) Add(bool update) {
		return this.Add(TValue: _data.Last, update: update);
	}
	public (DateTime t, double v) Add() {
		return Add(TValue: _data.Last, update: false);
	}
	private new void Sub(object source, TSeriesEventArgs e) {
		Add(TValue: _data.Last, update: e.update);
	}

	//reset calculation
	public override void Reset() {
		_len = 0;
	}
}