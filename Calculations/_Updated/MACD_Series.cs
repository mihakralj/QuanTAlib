namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
MACD: Moving Average Convergence/Divergence
    Moving average convergence divergence (MACD) is a trend-following momentum
    indicator that shows the relationship between two moving averages of a series.
    The MACD is calculated by subtracting the 26-period exponential moving average (EMA)
    from the 12-period EMA. MACD Signal is 9-day EMA of MACD.

</summary> */

public class MACD_Series : TSeries {
	private readonly System.Collections.Generic.List<double> _buffer = new();

	protected readonly int _slow, _fast, _signal;
	protected readonly bool _NaN;
	protected readonly TSeries _data;
	private readonly EMA_Series _TSlow;
	private readonly EMA_Series _TFast;
	public EMA_Series Signal { get; }

	//core constructors
	public MACD_Series(int slow = 26, int fast = 12, int signal = 9, bool useNaN = false) {
		_slow = slow;
		_fast = fast;
		_signal = signal;
		_NaN = useNaN;
		Name = $"MACD({slow},{fast},{signal})";
		_TSlow = new(slow, useNaN:false, useSMA:true);
		_TFast = new(fast, useNaN: false, useSMA: true);
		Signal = new(signal, useNaN: false, useSMA: true);
	}
	public MACD_Series(TSeries source, int slow, int fast, int signal, bool useNaN) : this(slow, fast, signal, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public MACD_Series(TSeries source) : this(source:source, slow:26, fast:12, signal:9 , useNaN:false) { }
	public MACD_Series(TSeries source, int slow, int fast, int signal) : this(source: source, slow: slow, fast:fast, signal:signal, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}

		var _sslow = _TSlow.Add(TValue,update);
		var _sfast = _TFast.Add(TValue, update);
		Signal.Add((TValue.t, _sfast.v-_sslow.v));

		var res = (TValue.t, Count < _fast - 1 && _NaN ? double.NaN : _sfast.v-_sslow.v);
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