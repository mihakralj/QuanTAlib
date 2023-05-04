namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
ADOSC: Chaikin Accumulation/Distribution Oscillator
    ADO measures the momentum of ADL using the difference between slow (10-day) EMA(ADL)
    and fast (3-day) EMA(ADL):

    Chaikin A/D Oscillator is defined as 3-day EMA of ADL  minus  10-day EMA of ADL

Sources:
    https://school.stockcharts.com/doku.php?id=technical_indicators:chaikin_oscillator

</summary> */

public class ADOSC_Series : TSeries {
	protected readonly TBars _data;
	private readonly double _k1, _k2;
	private double _lastema1, _lastlastema1, _lastema2, _lastlastema2;
	private double _lastadl, _lastlastadl;

	//core constructors
	public ADOSC_Series(int shortPeriod, int longPeriod, bool useNaN = false) {
		Name = $"ADOSC()";
		_k1 = 2.0 / (shortPeriod + 1);
		_k2 = 2.0 / (longPeriod + 1);
		_lastadl = _lastlastadl = _lastema1 = _lastlastema1 = _lastema2 = _lastlastema2 = 0;
	}
	public ADOSC_Series(TBars source, int shortPeriod, int longPeriod, bool useNaN = false) :this(shortPeriod, longPeriod, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_lastadl = _lastlastadl = 0;
		_data.Pub += Sub;
		Add(data: _data);
	}

	public ADOSC_Series() : this(shortPeriod: 3, longPeriod: 10, useNaN: false) {}

	public ADOSC_Series(TBars source) : this(source, shortPeriod: 3, longPeriod:10, useNaN:false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update= false) {

		if (update) {
			_lastadl = _lastlastadl;
			_lastema1 = _lastlastema1;
			_lastema2 = _lastlastema2;
		}

		double _adl = 0;
		double tmp = TBar.h - TBar.l;
		if (tmp > 0.0) { _adl = _lastadl + ((2 * TBar.c - TBar.l - TBar.h) / tmp * TBar.v); }
		if (this.Count == 0) { _lastema1 = _lastema2 = _adl; }

		double _ema1 = (_adl - _lastema1) * _k1 + _lastema1;
		double _ema2 = (_adl - _lastema2) * _k2 + _lastema2;

		_lastlastadl = _lastadl;
		_lastadl = _adl;
		_lastlastema1 = _lastema1;
		_lastema1 = _ema1;
		_lastlastema2 = _lastema2;
		_lastema2 = _ema2;

		double _adosc = _ema1 - _ema2;
		
		var ret = (TBar.t, _adosc);
		return base.Add(ret, update);
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
		_lastadl = _lastlastadl = _lastema1 = _lastlastema1 = _lastema2 = _lastlastema2 = 0;
	}
}