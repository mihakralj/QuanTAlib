namespace QuanTAlib;

using System;
using System.Linq;

/* <summary>
TEMA: Triple Exponential Moving Average
    TEMA uses EMA(EMA(EMA())) to calculate less laggy Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/triple-exponential-moving-average-tema/

Remark:
    ema1 = EMA(close, length)
    ema2 = EMA(ema1, length)
    ema3 = EMA(ema2, length)
    TEMA = 3 * (ema1 - ema2) + ema3

</summary> */

public class TEMA_Series : TSeries {
	private double _k;
	private double _sum, _oldsum;
	private double _lastema1, _oldema1, _lastema2, _oldema2, _lastema3, _oldema3;
	private int _len;
	private readonly bool _useSMA;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

//core constructor
	public TEMA_Series(int period, bool useNaN, bool useSMA) : base() {
		_period = period;
		_NaN = useNaN;
		_useSMA = useSMA;
		Name = $"TEMA({period})";
		_k = 2.0 / (_period + 1);
		_len = 0;
		_sum = _oldsum = _lastema1 = _lastema2 = _lastema3 = 0;
	}
	public TEMA_Series() : this(0, false, true) {}
	public TEMA_Series(int period) : this(period, false, true) {}
	public TEMA_Series(TBars source) : this(source.Close, 0, false) {}
	public TEMA_Series(TBars source, int period) : this(source.Close, period, false) {}
	public TEMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) {}
	public TEMA_Series(TSeries source, int period) : this(source, period, false, true) {}
	public TEMA_Series(TSeries source, int period, bool useNaN) : this(source, period, useNaN, true) {}
	public TEMA_Series(TSeries source, int period, bool useNaN, bool useSMA) : this(period, useNaN, useSMA) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	
// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (update) {
			_lastema1 = _oldema1;
			_lastema2 = _oldema2;
			_lastema3 = _oldema3;
			_sum = _oldsum;
		}
		else {
			_oldema1 = _lastema1;
			_oldema2 = _lastema2;
			_oldema3 = _lastema3;
			_oldsum = _sum;
			_len++;
		}

		if (_period == 0) { _k = 2.0 / (_len + 1); }

		double _ema1, _ema2, _ema3, _tema;
		if (this.Count == 0) {
			_ema1 = _ema2 = _ema3 =_sum = TValue.v;
		}
		else if (_len <= _period && _useSMA && _period != 0) {
			_sum += TValue.v;
			_ema1 = _sum / Math.Min(_len, _period);
			_ema2 = _ema1;
			_ema3 = _ema2;
		}
		else {
		_ema1 = (TValue.v - _lastema1) * _k + _lastema1;
		_ema2 = (_ema1 - _lastema2) * _k + _lastema2;
		_ema3 = (_ema2 - _lastema3) * _k + _lastema3;
		}

		_tema = (3 * (_ema1 - _ema2)) + _ema3;

		_lastema1 = Double.IsNaN(_ema1)?_lastema1:_ema1;
		_lastema2 = Double.IsNaN(_ema2)?_lastema2:_ema2;
		_lastema3 = Double.IsNaN(_ema3) ? _lastema3 : _ema3;

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _tema);
		return base.Add(res, update);
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
	
	//reset calculation
	public override void Reset() {
		_sum = _oldsum = _lastema1 = _lastema2 = 0;
		_len = 0;
	}
}