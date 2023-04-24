using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
WMAPE: Weighted Mean Absolute Percentage Error
    Measures the size of the error in percentage terms. Improves problems with MAPE
    when there are zero or close-to-zero values because there would be a division by zero 
    or values of MAPE tending to infinity.

Sources:
  https://en.wikipedia.org/wiki/WMAPE

</summary> */

public class WMAPE_Series : TSeries {
	private readonly System.Collections.Generic.List<double> _buffer = new();
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	//core constructors
	public WMAPE_Series(int period, bool useNaN) : base() {
		_period = period;
		_NaN = useNaN;
		Name = $"WMAPE({period})";
	}
	public WMAPE_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public WMAPE_Series() : this(period: 0, useNaN: false) { }
	public WMAPE_Series(int period) : this(period: period, useNaN: false) { }
	public WMAPE_Series(TBars source) : this(source.Close, 0, false) { }
	public WMAPE_Series(TBars source, int period) : this(source.Close, period, false) { }
	public WMAPE_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public WMAPE_Series(TSeries source) : this(source, 0, false) { }
	public WMAPE_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		BufferTrim(buffer:_buffer, value:TValue.v, period:_period, update: update);

		double _sma = _buffer.Average();

		double _div = 0;
		double _wmape = 0;
		for (int i = 0; i < _buffer.Count; i++) {
			_wmape += Math.Abs(_buffer[i] - _sma);
			_div += Math.Abs(_buffer[i]);
		}
		_wmape = (_div != 0) ? _wmape / _div : double.PositiveInfinity;

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _wmape);
		return base.Add(res, update);
	}

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
		_buffer.Clear();
	}
}