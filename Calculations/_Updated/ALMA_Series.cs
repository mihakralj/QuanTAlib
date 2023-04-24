namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
ALMA: Arnaud Legoux Moving Average
    The ALMA moving average uses the curve of the Normal (Gauss) distribution, which
    can be shifted from 0 to 1. This allows regulating the smoothness and high
    sensitivity of the indicator. Sigma is another parameter that is responsible for
    the shape of the curve coefficients. This moving average reduces lag of the data
    in conjunction with smoothing to reduce noise.


Sources:
    https://phemex.com/academy/what-is-arnaud-legoux-moving-averages
    https://www.prorealcode.com/prorealtime-indicators/alma-arnaud-legoux-moving-average/

    Discrepancy with Pandas-TA (but passes the validation with Skender.GetAlma)
</summary> */

public class ALMA_Series : TSeries {
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	private readonly System.Collections.Generic.List<double> _buffer = new();
	private readonly System.Collections.Generic.List<double> _weight = new();
	private double _norm;
	private readonly double _offset, _sigma;

	//core constructors
	public ALMA_Series(int period, double offset, double sigma, bool useNaN) : base() {
		_period = period;
		_NaN = useNaN;
		Name = $"ALMA({period})";
		_offset = offset;
		_sigma = sigma;
		_weight = new();
	}
	public ALMA_Series(TSeries source, int period, double offset, double sigma, bool useNaN) : this(period, offset, sigma, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}

	public ALMA_Series() : this(period:0, offset:0.85, sigma:6.0, useNaN: false) { }
	public ALMA_Series(int period) : this(period: period, offset:0.85, sigma:6.0, useNaN:false) { }
	public ALMA_Series(TBars source) : this(source:source.Close, period:0, offset:0.85, sigma:6.0, useNaN:false) { }
	public ALMA_Series(TBars source, int period) : this(source:source.Close, period:period, offset: 0.85, sigma: 6.0, useNaN: false) { }
	public ALMA_Series(TBars source, int period, double offset, double sigma, bool useNaN) : this(source.Close, period:period, offset: offset, sigma: sigma, useNaN: false) { }
	public ALMA_Series(TSeries source) : this(source, period:0, offset:0.85, sigma:6.0, useNaN:false) { }
	public ALMA_Series(TSeries source, int period) : this(source:source, period:period, offset:0.85, sigma:6.0, useNaN:false) { }
	public ALMA_Series(TSeries source, int period, bool useNaN) : this(source: source, period: period, offset: 0.85, sigma: 6.0, useNaN: useNaN) { }

	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update=false) {
		BufferTrim(_buffer, TValue.v, _period, update);
		 if (_weight.Count < _buffer.Count) {
            for (int i = 0; i < (_buffer.Count - _weight.Count); i++) { _weight.Add(0.0); }
        }
		if (this._buffer.Count <= _period || _period ==0) {
			int _len = this._buffer.Count;
			_norm = 0;
			double _m = _offset * (_len - 1);
			double _s = _len / _sigma;
			for (int i = 0; i < _len; i++) {
				double _wt = Math.Exp(-((i - _m) * (i - _m)) / (2 * _s * _s));
				_weight[i] = _wt;
				_norm += _wt;
			}
		}

		double _weightedSum = 0;
		for (int i = 0; i < this._buffer.Count; i++) { _weightedSum += _weight[i] * _buffer[i]; }
		double _alma = _weightedSum / _norm;

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _alma);
		return base.Add(res, update);
	}

	//reset calculation
	public override void Reset() {
		_buffer.Clear();
		_weight.Clear();
	}

	//variation of Add()
	public override (DateTime t, double v) Add(TSeries data) {
		if (data == null) { return (DateTime.Today, Double.NaN); }
		foreach (var item in data) { Add(item, false); }
		return _data.Last;
	}
	public new (DateTime t, double v) Add((DateTime t, double v) TValue) {
		return Add(TValue, false);
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
}