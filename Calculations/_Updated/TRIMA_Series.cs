namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
TRIMA: Triangular Moving Average
    A weighted moving average where the shape of the weights are triangular and the greatest
    weight is in the middle of the period,

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/triangular-moving-average-trima/

Remark:
    trima = sma(sma(signal, n/2), n/2)

</summary> */

public class TRIMA_Series : TSeries {
	private readonly int _p1a, _p1b;
	private SMA_Series sma, trima;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	//core constructors
	public TRIMA_Series(int period, bool useNaN) : base() {
		_period = period;
		_NaN = useNaN;
		Name = $"xMA({period})";
		_p1a = (int)Math.Floor((period * 0.5) + 1);
		_p1b = (int)Math.Ceiling(0.5 * period);
		sma = new(_p1a);
		trima = new(_p1b);

	}
	public TRIMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public TRIMA_Series() : this(period: 0, useNaN: false) { }
	public TRIMA_Series(int period) : this(period: period, useNaN: false) { }
	public TRIMA_Series(TBars source) : this(source.Close, 0, false) { }
	public TRIMA_Series(TBars source, int period) : this(source.Close, period, false) { }
	public TRIMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public TRIMA_Series(TSeries source) : this(source, 0, false) { }
	public TRIMA_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}

		var _sma = sma.Add(TValue, update);
		var _trima = trima.Add(_sma, update);

		var res = (_trima.t, Count < _period - 1 && _NaN ? double.NaN : _trima.v);
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
		sma.Reset();
		trima.Reset();
	}
}