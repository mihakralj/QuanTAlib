using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
SSDEV: (Corrected) Sample Standard Deviation
  Sample Standard Deviaton uses Bessel's correction to correct the bias in the variance.

Sources:
  https://en.wikipedia.org/wiki/Standard_deviation#Corrected_sample_standard_deviation
  Bessel's correction: https://en.wikipedia.org/wiki/Bessel%27s_correction

Remark:
  SSDEV (Sample Standard Deviation) is also known as a unbiased/corrected Standard Deviation.
  For a population/biased/uncorrected Standard Deviation, use PSDEV instead

</summary> */

public class SSDEV_Series : TSeries {
	private readonly System.Collections.Generic.List<double> _buffer = new();
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	//core constructors
	public SSDEV_Series(int period, bool useNaN) {
		_period = period;
		_NaN = useNaN;
		Name = $"SSDEV({period})";
	}
	public SSDEV_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public SSDEV_Series() : this(period: 0, useNaN: false) { }
	public SSDEV_Series(int period) : this(period: period, useNaN: false) { }
	public SSDEV_Series(TBars source) : this(source.Close, 0, false) { }
	public SSDEV_Series(TBars source, int period) : this(source.Close, period, false) { }
	public SSDEV_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public SSDEV_Series(TSeries source) : this(source, 0, false) { }
	public SSDEV_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		BufferTrim(buffer:_buffer, value:TValue.v, period:_period, update: update);

		double _sma = _buffer.Average();

		double _svar = 0;
		for (int i = 0; i < this._buffer.Count; i++) { _svar += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
		_svar /= (_buffer.Count > 1) ? _buffer.Count - 1 : 1; // Bessel's correction
		double _ssdev = Math.Sqrt(_svar);

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _ssdev);
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