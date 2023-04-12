namespace QuanTAlib;
using System;
using System.Linq;
using System.Numerics;

/* <summary>
T3: Tillson T3 Moving Average
    Tim Tillson described it in "Technical Analysis of Stocks and Commodities", January 1998 in the
    article "Better Moving Averages". Tillson’s moving average becomes a popular indicator of
    technical analysis as it gets less lag with the price chart and its curve is considerably smoother.

Sources:
    https://technicalindicators.net/indicators-technical-analysis/150-t3-moving-average
    http://www.binarytribune.com/forex-trading-indicators/t3-moving-average-indicator/

</summary> */
public class T3_Series : Single_TSeries_Indicator {
	private readonly double _k, _k1m, _c1, _c2, _c3, _c4;
	private readonly System.Collections.Generic.List<double> _buffer1 = new();
	private readonly System.Collections.Generic.List<double> _buffer2 = new();
	private readonly System.Collections.Generic.List<double> _buffer3 = new();
	private readonly System.Collections.Generic.List<double> _buffer4 = new();
	private readonly System.Collections.Generic.List<double> _buffer5 = new();
	private readonly System.Collections.Generic.List<double> _buffer6 = new();

	private double _lastema1, _lastema2, _lastema3, _lastema4, _lastema5, _lastema6;
	private double _llastema1, _llastema2, _llastema3, _llastema4, _llastema5, _llastema6;
	private readonly bool _useSMA;

	public T3_Series(TSeries source, int period, double vfactor = 0.7, bool useNaN = false, bool useSMA = true) : base(source, period, useNaN) {
		double _a = vfactor; //0.7; //0.618
		_c1 = -_a * _a * _a;
		_c2 = 3 * _a * _a + 3 * _a * _a * _a;
		_c3 = -6 * _a * _a - 3 * _a - 3 * _a * _a * _a;
		_c4 = 1 + 3 * _a + _a * _a * _a + 3 * _a * _a;

		_k = 2.0 / (_p + 1);
		_k1m = 1.0 - _k;
		_lastema1 = _llastema1 = _lastema2 = _llastema2 = _lastema3 = _llastema3 = _lastema4 = _llastema4 = _lastema5 = _llastema5 = _lastema5 = _llastema5 = 0;
		_useSMA = useSMA;
		if (this._data.Count > 0) { base.Add(this._data); }
	}

	public override void Add((DateTime t, double v) TValue, bool update) {
		double _ema1, _ema2, _ema3, _ema4, _ema5, _ema6;
		if (update) { _lastema1 = _llastema1; _lastema2 = _llastema2; _lastema3 = _llastema3; _lastema4 = _llastema4; _lastema5 = _llastema5; _lastema6 = _llastema6; }
		else { _llastema1 = _lastema1; _llastema2 = _lastema2; _llastema3 = _lastema3; _llastema4 = _lastema4; _llastema5 = _lastema5; _llastema6 = _lastema6; }

		if (this.Count == 0) { _lastema1 = _lastema2 = _lastema3 = _lastema4 = _lastema5 = _lastema6 = TValue.v; }

		if ((this.Count < _p) && _useSMA) {
			Add_Replace(_buffer1, TValue.v, update);
			_ema1 = 0;
			for (int i = 0; i < _buffer1.Count; i++) { _ema1 += _buffer1[i]; }
			_ema1 /= _buffer1.Count;

			Add_Replace(_buffer2, _ema1, update);
			_ema2 = 0;
			for (int i = 0; i < _buffer2.Count; i++) { _ema2 += _buffer2[i]; }
			_ema2 /= _buffer2.Count;

			Add_Replace(_buffer3, _ema2, update);
			_ema3 = 0;
			for (int i = 0; i < _buffer3.Count; i++) { _ema3 += _buffer3[i]; }
			_ema3 /= _buffer3.Count;

			Add_Replace(_buffer4, _ema3, update);
			_ema4 = 0;
			for (int i = 0; i < _buffer4.Count; i++) { _ema4 += _buffer4[i]; }
			_ema4 /= _buffer4.Count;

			Add_Replace(_buffer5, _ema4, update);
			_ema5 = 0;
			for (int i = 0; i < _buffer5.Count; i++) { _ema5 += _buffer5[i]; }
			_ema5 /= _buffer5.Count;

			Add_Replace(_buffer6, _ema5, update);
			_ema6 = 0;
			for (int i = 0; i < _buffer6.Count; i++) { _ema6 += _buffer6[i]; }
			_ema6 /= _buffer6.Count;
		}
		else {
			_ema1 = (TValue.v * this._k) + (this._lastema1 * this._k1m);
			_ema2 = (_ema1 * this._k) + (this._lastema2 * this._k1m);
			_ema3 = (_ema2 * this._k) + (this._lastema3 * this._k1m);
			_ema4 = (_ema3 * this._k) + (this._lastema4 * this._k1m);
			_ema5 = (_ema4 * this._k) + (this._lastema5 * this._k1m);
			_ema6 = (_ema5 * this._k) + (this._lastema6 * this._k1m);
		}
		_lastema1 = _ema1;
		_lastema2 = _ema2;
		_lastema3 = _ema3;
		_lastema4 = _ema4;
		_lastema5 = _ema5;
		_lastema6 = _ema6;

		double _T3 = _c1 * _ema6 + _c2 * _ema5 + _c3 * _ema4 + _c4 * _ema3;
		base.Add((TValue.t, _T3), update, _NaN);
	}
}