using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
MAE: Mean Absolute Error 
    Defined as a Mean (Average) of the absolute difference between actual and estimated values.
		MAE = (1/n) * Σ|y_i - MA_i|

Sources:
  https://en.wikipedia.org/wiki/Mean_absolute_error

</summary> */

public class MAE_Series : TSeries {
	private readonly System.Collections.Generic.List<double> _buffer = new();
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	//core constructors
	public MAE_Series(int period, bool useNaN) {
		_period = period;
		_NaN = useNaN;
		Name = $"MSE({period})";
	}
	public MAE_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public MAE_Series() : this(period: 0, useNaN: false) { }
	public MAE_Series(int period) : this(period: period, useNaN: false) { }
	public MAE_Series(TBars source) : this(source.Close, 0, false) { }
	public MAE_Series(TBars source, int period) : this(source.Close, period, false) { }
	public MAE_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public MAE_Series(TSeries source) : this(source, 0, false) { }
	public MAE_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		BufferTrim(buffer:_buffer, value:TValue.v, period:_period, update: update);

		double _sma = _buffer.Average();

		double _mae = 0;
		for (int i = 0; i < _buffer.Count; i++) { _mae += Math.Abs(_buffer[i] - _sma); }
		_mae /= this._buffer.Count;

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _mae);
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
		_buffer.Clear();
	}
}