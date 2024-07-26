namespace QuanTAlib;

using System;
using System.Linq;

/* <summary>
HWMA: Holt-Winter Moving Average
		Indicator HWMA (Holt-Winter Moving Average) is a three-parameter moving 
		average by the Holt-Winter method; Holt-Winters Exponential Smoothing is 
		used for forecasting time series data that exhibits both a trend and a 
		seasonal variation.
    

Sources:
		https://timeseriesreasoning.com/contents/holt-winters-exponential-smoothing/
    https://www.mql5.com/en/code/20856

nA - smoothed series (from 0 to 1)
nB - assess the trend (from 0 to 1)
nC - assess seasonality (from 0 to 1)

Heuristic for determining alpha, beta, and gamma from period:
    alpha = 2 / (1 + period)
    beta = 1 / period
    gamma = 1 / period

F[i] = (1-nA) * (F[i-1] + V[i-1] + 0.5 * A[i-1]) + nA * Price[i]
V[i] = (1-nB) * (V[i-1] + A[i-1]) + nB * (F[i] - F[i-1])
A[i] = (1-nC) * A[i-1] + nC * (V[i] - V[i-1])
HWMA[i] = F[i] + V[i] + 0.5 * A[i]

</summary> */

public class HWMA_Series : TSeries {
	private int _len;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;
	double _nA, _nB, _nC;
	double _pF, _pV, _pA;
	double _ppF, _ppV, _ppA;

	//core constructors

	public HWMA_Series(double nA, double nB, double nC, bool useNaN) {
		_period = (int)((2 - nA) / nA);
		_nA = nA;
		_nB = nB;
		_nC = nC;
		_NaN = useNaN;
		Name = $"HWMA({_period})";
		_len = 0;
	}
	public HWMA_Series(TSeries source, double nA, double nB, double nC, bool useNaN = false) : this(nA, nB, nC, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public HWMA_Series() : this(period: 0, useNaN: false) { }
	public HWMA_Series(int period) : this(period, useNaN: false) { }
	public HWMA_Series(int period, bool useNaN) : this(nA: 2 / (1 + (double)period), nB: 1 / (double)period, nC: 1 / (double)period, useNaN) {
		_period = period;
	}
	public HWMA_Series(TBars source) : this(source.Close, period: 0, useNaN: false) { }
	public HWMA_Series(TBars source, int period) : this(source.Close, period, false) { }
	public HWMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public HWMA_Series(TSeries source, int period) : this(source, period, false) { }
	public HWMA_Series(TSeries source, int period, bool useNaN) : this(source, nA: 2 / (1 + (double)period), nB: 1 / (double)period, nC: 1 / (double)period, useNaN: useNaN) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (double.IsNaN(TValue.v)) {
			return base.Add((TValue.t, Double.NaN), update);
		}
		double _F, _V, _A;
		if (_len == 0) { _pF = TValue.v; _pA = _pV = 0; }

		if (update) { _pF = _ppF; _pV = _ppV; _pA = _ppA; }
		else {
			_ppF = _pF;
			_ppV = _pV;
			_ppA = _pA;
			_len++;
		}

		if (_period == 0) {
			_nA = 2 / (1 + (double)_len);
			_nB = 1 / (double)_len;
			_nC = 1 / (double)_len;
		}
		if (_period == 1) {
			_nA = 1;
			_nB = 0;
			_nC = 0;
		}

		_F = (1 - _nA) * (_pF + _pV + 0.5 * _pA) + _nA * TValue.v;
		_V = (1 - _nB) * (_pV + _pA) + _nB * (_F - _pF);
		_A = (1 - _nC) * _pA + _nC * (_V - _pV);

		double _hwma = _F + _V + 0.5 * _A;
		_pF = _F;
		_pV = _V;
		_pA = _A;

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _hwma);
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
		_len = 0;
	}
}