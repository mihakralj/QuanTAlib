namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
KAMA: Kaufman's Adaptive Moving Average
    Created in 1988 by American quantitative finance theorist Perry J. Kaufman and is known as
    Kaufman's Adaptive Moving Average (KAMA). Even though the method was developed as early as 1972,
    it was not until the popular book titled "Trading Systems and Methods" that it was made widely
    available to the public. Unlike other conventional moving averages systems, the Kaufman's Adaptive
    Moving Average, considers market volatility apart from price fluctuations.

    KAMA[i] = KAMA[i-1] + SC * ( price - KAMA[i-1] )

Sources:
    https://www.tutorialspoint.com/kaufman-s-adaptive-moving-average-kama-formula-and-how-does-it-work
    https://corporatefinanceinstitute.com/resources/knowledge/trading-investing/kaufmans-adaptive-moving-average-kama/
    https://www.technicalindicators.net/indicators-technical-analysis/152-kama-kaufman-adaptive-moving-average

Remark:
    If useNaN:true argument is provided, KAMA starts calculating values from [period] bar onwards.
    Without useNaN argument (default setting), KAMA starts calculating values from bar 1 - and yields
    slightly different results for the first 50 bars - and then converges with the other one.

</summary> */

public class KAMA_Series : TSeries {
	private readonly System.Collections.Generic.List<double> _buffer = new();
	private double _lastkama, _lastlastkama;
	private int _len;
	private readonly double _scFast, _scSlow;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	//core constructors
	public KAMA_Series(int period, int fast, int slow, bool useNaN) : base() {
		_period = period;
		_NaN = useNaN;
		_len = 0;
		_scFast = 2.0 / (((period < fast) ? period : fast) + 1);
		_scSlow = 2.0 / (slow + 1);
		_lastkama = _lastlastkama = 0;
		Name = $"KAMA({period})";
	}
	public KAMA_Series(TSeries source, int period, int fast, int slow, bool useNaN) : this(period, fast, slow, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public KAMA_Series() : this(period: 0, fast: 2, slow: 30, useNaN: false) { }
	public KAMA_Series(int period) : this(period: period, fast: 2, slow: 30, useNaN: false) { }
	public KAMA_Series(TBars source) : this(source.Close, period: 0, fast: 2, slow: 30, useNaN: false) { }
	public KAMA_Series(TBars source, int period) : this(source.Close, period: period, fast: 2, slow: 30, useNaN: false) { }
	public KAMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period: period, fast: 2, slow: 30, useNaN: useNaN) { }
	public KAMA_Series(TSeries source) : this(source, period: 0, fast: 2, slow: 30, useNaN: false) { }
	public KAMA_Series(TSeries source, int period) : this(source: source, period: period, fast: 2, slow: 30, useNaN: false) { }
	public KAMA_Series(TSeries source, int period, bool useNaN) : this(source: source, period: period, fast: 2, slow: 30, useNaN: useNaN) { }
	public KAMA_Series(TSeries source, int period, int fast, int slow) : this(source: source, period: period, fast: fast, slow: slow, useNaN: false) { }


	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}

		if (update) { _lastkama = _lastlastkama; }
		else { _lastlastkama = _lastkama; }
		BufferTrim(buffer: _buffer, value: TValue.v, period: _period + 1, update: update);

		double _kama = 0;
		if (this.Count < _period) { _kama = TValue.v; }
		else {
			double _change = Math.Abs(_buffer[^1] - _buffer[(_buffer.Count > _period + 1) ? 1 : 0]);
			double _sumpv = 0;
			for (int i = 1; i < _buffer.Count; i++) { _sumpv += Math.Abs(_buffer[(_buffer.Count > 0) ? i : 0] - _buffer[i - 1]); }
			double _er = (_sumpv == 0) ? 0 : _change / _sumpv;
			double _sc = (_er * (_scFast - _scSlow)) + _scSlow;
			_kama = (_lastkama + (_sc * _sc * (TValue.v - _lastkama)));
		}
		_len++;
		_lastkama = _kama;
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _kama);
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
		_len = 0;
		_lastkama = _lastlastkama = 0;
	}
}