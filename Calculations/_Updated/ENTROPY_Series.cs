namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
ENTROPY:
    Introduced by Claude Shannon in 1948, entropy measures the unpredictability
    of the data, or equivalently, of its average information.

Calculation:
    P = close / Σ(close)
    ENTROPY = Σ(-P * Log(P) / Log(base))

Sources:
    https://en.wikipedia.org/wiki/Entropy_(information_theory)
    https://math.stackexchange.com/questions/3428693/how-to-calculate-entropy-from-a-set-of-correlated-samples

</summary> */

public class ENTROPY_Series : TSeries {
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;
	private readonly double _logbase;
	private readonly System.Collections.Generic.List<double> _buffer = new();
	private readonly System.Collections.Generic.List<double> _buff2 = new();

	//core constructors
	public ENTROPY_Series(int period, double logbase, bool useNaN) {
		_period = period;
		_NaN = useNaN;
		_logbase = logbase;
		Name = $"ENTROPY({period})";
	}
	public ENTROPY_Series(TSeries source, int period, double logbase, bool useNaN) : this(period, logbase, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public ENTROPY_Series() : this(period: 0, logbase: 2.0, useNaN: false) { }
	public ENTROPY_Series(int period) : this(period: period, logbase: 2.0, useNaN: false) { }
	public ENTROPY_Series(TBars source) : this(source.Close, period: 0, logbase: 2.0, useNaN: false) { }
	public ENTROPY_Series(TBars source, int period) : this(source.Close, period, logbase: 2.0, useNaN: false) { }
	public ENTROPY_Series(TBars source, int period, bool useNaN) : this(source.Close, period: period, logbase: 2.0, useNaN: useNaN) { }
	public ENTROPY_Series(TSeries source) : this(source, period: 0, logbase: 2.0, useNaN: false) { }
	public ENTROPY_Series(TSeries source, int period) : this(source: source, period: period, logbase: 2.0, useNaN: false) { }
	public ENTROPY_Series(TSeries source, int period, bool useNaN) : this(source: source, period: period, logbase: 2.0, useNaN: useNaN) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}
		BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);
		double _sum = _buffer.Sum();
		double _pp = this._buffer[^1] / _sum;
		double _ppp = -_pp * Math.Log(_pp) / Math.Log(this._logbase);
		BufferTrim(_buff2, _ppp, _period, update);
		double _entp = _buff2.Sum();
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _entp);
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
		_buff2.Clear();
	}
}