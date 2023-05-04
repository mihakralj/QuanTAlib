namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
SMA: Simple Moving Average
    The weights are equally distributed across the period, resulting in a mean() of
    the data within the period

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/simple-moving-average-sma/
    https://stats.stackexchange.com/a/24739

Remark:
    This calc doesn't use LINQ or SUM() or any of (slow) iterative methods. It is not as fast as TA-LIB
    implementation, but it does allow incremental additions of inputs and real-time calculations of SMA()

</summary> */
public class SMA_Series : TSeries {
	private readonly System.Collections.Generic.List<double> _buffer = new();

	private double _sum, _oldsum;
	private readonly int _period;
	private readonly TSeries _data;
	protected readonly bool _NaN;

	//core constructor
	public SMA_Series(int period, bool useNaN) {
		_period = Math.Max(0, period);
		_NaN = useNaN;
		Name = $"SMA({period})";
		_sum = _oldsum = 0;
	}
	public SMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public SMA_Series() : this(0, false) {}
	public SMA_Series(int period) : this(period, false) {}
	public SMA_Series(TBars source) : this(source.Close, 0, false) {}
	public SMA_Series(TBars source, int period) : this(source.Close, period, false) {}
	public SMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) {}
	public SMA_Series(TSeries source) : this(source, 0, false) {}
	public SMA_Series(TSeries source, int period) : this(source, period, false) {}

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) { return (TValue.t, double.NaN);
		} else {
			if (update && _buffer.Count > 0) {
				_sum -= _buffer[^1];
				_buffer[^1] = TValue.v;
				_oldsum = _sum;
			}
			else {
				_buffer.Add(TValue.v);
				_oldsum = _sum;
			}

			_sum += TValue.v;
			if (_period != 0 && _buffer.Count > _period) {
				_sum -= _buffer[0];
				_buffer.RemoveAt(0);
			}
		}

		double _div = _period == 0 ? _buffer.Count : Math.Min(_buffer.Count, _period);
		var _sma = _sum / _div;
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _sma);
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
		_sum = _oldsum = 0;
		_buffer.Clear();
	}
}