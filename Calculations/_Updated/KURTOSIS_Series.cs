namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
KURTOSIS: Kurtosis of population
  Kurtosis characterizes the relative peakedness or flatness of a distribution
  compared with the normal distribution. Positive kurtosis indicates a relatively
  peaked distribution. Negative kurtosis indicates a relatively flat distribution.

  The normal curve is called Mesokurtic curve. If the curve of a distribution is
  more outlier prone (or heavier-tailed) than a normal or mesokurtic curve then
  it is referred to as a Leptokurtic curve. If a curve is less outlier prone (or
  lighter-tailed) than a normal curve, it is called as a platykurtic curve.

Calculation:
    sum4 = Σ(close-SMA)^4
    sum2 = (Σ(close-SMA)^2)^2
    KURTOSIS = length * (sum4/sum2)

Sources:
  https://en.wikipedia.org/wiki/Kurtosis
  https://stats.oarc.ucla.edu/other/mult-pkg/faq/general/faq-whats-with-the-different-formulas-for-kurtosis/

</summary> */

public class KURTOSIS_Series : TSeries {
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;
	private readonly System.Collections.Generic.List<double> _buffer = new();

	//core constructors
	public KURTOSIS_Series(int period, bool useNaN) : base() {
		_period = period;
		_NaN = useNaN;
		Name = $"KURTOSIS({period})";
	}
	public KURTOSIS_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public KURTOSIS_Series() : this(period: 0, useNaN: false) { }
	public KURTOSIS_Series(int period) : this(period: period, useNaN: false) { }
	public KURTOSIS_Series(TSeries source) : this(source, period: 0, useNaN: false) { }
	public KURTOSIS_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}

		BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);
		double _n = _buffer.Count;
		double _avg = _buffer.Average();

		double _s2 = 0;
		double _s4 = 0;
		for (int i = 0; i < this._buffer.Count; i++) {
			_s2 += (_buffer[i] - _avg) * (_buffer[i] - _avg);
			_s4 += (_buffer[i] - _avg) * (_buffer[i] - _avg) * (_buffer[i] - _avg) * (_buffer[i] - _avg);
		}

		double _Vx = _s2 / (_n - 1);
		double _kurt = (_n > 3) ?
	(_n * (_n + 1) * _s4) / (_Vx * _Vx * (_n - 3) * (_n - 1) * (_n - 2)) - (3 * (_n - 1) * (_n - 1) / ((_n - 2) * (_n - 3))) //using Sheskin Algo
	: (_s2 * _s2) / _n - 3; //using Snedecor and Cochran (1967) algo
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _kurt);
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