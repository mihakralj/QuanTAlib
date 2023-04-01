namespace QuanTAlib;
using System;

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

F[i] = (1-nA) * (F[i-1] + V[i-1] + 0.5 * A[i-1]) + nA * Price[i]
V[i] = (1-nB) * (V[i-1] + A[i-1]) + nB * (F[i] - F[i-1])
A[i] = (1-nC) * A[i-1] + nC * (V[i] - V[i-1])
HWMA[i] = F[i] + V[i] + 0.5 * A[i]

</summary> */

public class HWMA_Series : Single_TSeries_Indicator {
	double _nA, _nB, _nC;
	double _pF, _pV, _pA;
	double _ppF, _ppV, _ppA;

	public HWMA_Series(TSeries source, double nA = 0.2, double nB = 0.1, double nC = 0.1, bool useNaN = false) : base(source, 0, useNaN) {

		_nA = nA;
		_nB = nB;
		_nC = nC;
		if (this._data.Count > 0) { base.Add(this._data); }
	}
	public override void Add((DateTime t, double v) TValue, bool update) {
		double _F, _V, _A;
		if (this.Count == 0) { _pF = TValue.v; _pA = _pV = 0; }

		if (update) { _pF = _ppF; _pV = _ppV; _pA = _ppA; }
		else { _ppF = _pF; _ppV = _pV; _ppA = _pA; }

		_F = (1 - _nA) * (_pF + _pV + 0.5 * _pA) + _nA * TValue.v;
		_V = (1 - _nB) * (_pV + _pA) + _nB * (_F - _pF);
		_A = (1 - _nC) * _pA + _nC * (_V - _pV);

		double _hwma = _F + _V + 0.5 * _A;
		_pF = _F;
		_pV = _V;
		_pA = _A;

		base.Add((TValue.t, _hwma), update, _NaN);
	}
}
