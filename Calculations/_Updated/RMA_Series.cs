namespace QuanTAlib;

using System;
using System.Linq;

/* <summary>
RMA: wildeR Moving Average
    J. Welles Wilder introduced RMA as an alternative to EMA. RMA's weight (k) is
    set as 1/period, giving less weight to the new data compared to EMA.

Sources:
    https://archive.org/details/newconceptsintec00wild/page/23/mode/2up
	https://tlc.thinkorswim.com/center/reference/Tech-Indicators/studies-library/V-Z/WildersSmoothing
    https://www.incrediblecharts.com/indicators/wilder_moving_average.php

Issues:
    Pandas-TA library calculates RMA using straight Exponential Weighted Mean:
	pandas.ewm().mean() and returns incorrect first (period) of bars compared to
	published formula. This implementation passess the validation test in Wilder's book.

</summary> */

public class RMA_Series : TSeries {
	private double _k;
	private double _lastrma, _oldrma;
	private double _sum, _oldsum;
	private readonly bool _useSMA;
	private int _len;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

//core constructor
	public RMA_Series(int period, bool useNaN, bool useSMA) {
		_period = period;
		_NaN = useNaN;
		_useSMA = useSMA;
		Name = $"RMA({period})";
		_k = 1.0 / (double)(this._period);
		_len = 0;
		_sum = _oldsum = _lastrma = _oldrma = 0;
	}
	//generic constructors (source)

	public RMA_Series() : this(0, false, true) {}
	public RMA_Series(int period) : this(period, false, true) {}
	public RMA_Series(TBars source) : this(source.Close, 0, false) {}
	public RMA_Series(TBars source, int period) : this(source.Close, period, false) {}
	public RMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) {}
	public RMA_Series(TSeries source, int period) : this(source, period, false, true) {}
	public RMA_Series(TSeries source, int period, bool useNaN) : this(source, period, useNaN, true) {}
	public RMA_Series(TSeries source, int period, bool useNaN, bool useSMA) : this(period, useNaN, useSMA) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}

// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (update) {
			_lastrma = _oldrma;
			_sum = _oldsum;
		}
		else {
			_oldrma = _lastrma;
			_oldsum = _sum;
			_len++;
		}

		double _rma = 0;
		if (_period == 0) {
			_k = 1.0 / (double)(this._len);
		}

		if (Count == 0) {
			_rma = _sum = TValue.v;

		} else if (_len <= _period && _useSMA && _period != 0) {
			_sum += TValue.v;
			if (_period != 0 && _len > _period) {
				_sum -= _data[Count - _period - (update ? 1 : 0)].v;
			}
			_rma = _sum / Math.Min(_len, _period);
		}
		else {
			_rma = _k * (TValue.v - _lastrma) + _lastrma;
		}

		_lastrma = double.IsNaN(_rma) ? _lastrma : _rma;
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _rma);
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
		_sum = _oldsum = _lastrma = _oldrma = 0;
		_len = 0;
	}
}