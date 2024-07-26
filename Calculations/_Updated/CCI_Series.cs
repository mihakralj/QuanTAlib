namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
CCI: Commodity Channel Index 
    Commodity Channel Index is a momentum oscillator used to primarily identify overbought
    and oversold levels relative to a mean. CCI measures the current price level relative
    to an average price level over a given period of time:
    - CCI is relatively high when prices are far above their average.
    - CCI is relatively low when prices are far below their average.
    Using this method, CCI can be used to identify overbought and oversold levels.

Sources:
    https://www.investopedia.com/terms/c/commoditychannelindex.asp
    https://www.fidelity.com/learning-center/trading-investing/technical-analysis/technical-indicator-guide/cci

</summary> */

public class CCI_Series : TSeries {
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TBars _data;
	private readonly System.Collections.Generic.List<double> _tp = new();

	//core constructors
	public CCI_Series(int period, bool useNaN) {
		_period = period;
		_NaN = useNaN;
		Name = $"CCI({period})";
	}
	public CCI_Series(TBars source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(data: _data);
	}
	public CCI_Series() : this(period: 2, useNaN: false) { }
	public CCI_Series(int period) : this(period: period, useNaN: false) { }
	public CCI_Series(TBars source) : this(source, period: 2, useNaN: false) { }
	public CCI_Series(TBars source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false) {
		double _tpItem = (TBar.h + TBar.l + TBar.c) / 3.0;
		if (update) {
			this._tp[this._tp.Count - 1] = _tpItem;
		}
		else { 
			this._tp.Add(_tpItem);
		}
		if (this._tp.Count > this._period) { this._tp.RemoveAt(0); }

		// average TP over _tp buffer
		double _avgTp = _tp.Average();

		// average Deviation over _tp buffer
		double _avgDv = 0;
		for (int i = 0; i < this._tp.Count; i++) { _avgDv += Math.Abs(_avgTp - this._tp[i]); }
		_avgDv /= this._tp.Count;

		double _cci = (_avgDv == 0) ? 0 : (this._tp[this._tp.Count - 1] - _avgTp) / (0.015 * _avgDv);
		var res = (TBar.t, Count < _period - 1 && _NaN ? double.NaN : _cci);
		return base.Add(res, update);
	}

	public new void Add(TBars data) {
		foreach (var item in data) { Add(item, false); }
	}
	public (DateTime t, double v) Add(bool update) {
		return this.Add(TBar: _data.Last, update: update);
	}
	public (DateTime t, double v) Add() {
		return Add(TBar: _data.Last, update: false);
	}
	private new void Sub(object source, TSeriesEventArgs e) {
		Add(TBar: _data.Last, update: e.update);
	}

	//reset calculation
	public override void Reset() {
		_tp.Clear();
	}
}