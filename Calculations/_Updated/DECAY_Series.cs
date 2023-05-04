namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
DECAY: 
  Linear decay can be modeled by a straight line with a negative slope of 1/period.
	The value decreases in a straight line from the last maximum to 0.
	Decay = Last Max - distance/period

	Exponential decay  is modeled as an exponential curve with diminishing factor of
	1-1/p

</summary> */

public class DECAY_Series : TSeries {
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;
	private readonly bool _exp;
	private double _pdecay, _ppdecay;
	private readonly double _dfactor;

	//core constructors
	public DECAY_Series(int period, bool exponential, bool useNaN) {
		_period = period;
		_NaN = useNaN;
		Name = $"DECAY({period})";
		_exp = exponential;
		_dfactor = (_exp) ? 1.0 - 1.0 / (double)_period : 1 / (double)_period;
		_pdecay = _ppdecay = 0;
	}
	public DECAY_Series(TSeries source, int period, bool exponential, bool useNaN) : this(period, exponential, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public DECAY_Series() : this(period: 0, exponential: false, useNaN: false) { }
	public DECAY_Series(int period) : this(period: period, exponential: false, useNaN: false) { }
	public DECAY_Series(TBars source) : this(source.Close, period: 0, exponential: false, useNaN: false) { }
	public DECAY_Series(TBars source, int period) : this(source.Close, period: period, exponential:false, useNaN:false) { }
	public DECAY_Series(TBars source, int period, bool useNaN) : this(source.Close, period: period, exponential: false, useNaN) { }
	public DECAY_Series(TSeries source) : this(source, period: 0, exponential: false, useNaN:false) { }
	public DECAY_Series(TSeries source, int period) : this(source: source, period: period, exponential: false, useNaN: false) { }
	public DECAY_Series(TSeries source, int period, bool useNaN) : this(source: source, period: period, exponential: false, useNaN: useNaN) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}
		if (update) { _pdecay = _ppdecay; }
		else { _ppdecay = _pdecay; }

		if (this.Count == 0) { _pdecay = TValue.v; }
		double _decay = Math.Max(TValue.v, Math.Max((_exp) ? _pdecay * _dfactor : _pdecay - _dfactor, 0));
		_pdecay = _decay;
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _decay);
		return base.Add(res, update);
	}

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
		_pdecay = _ppdecay = 0;
	}
}