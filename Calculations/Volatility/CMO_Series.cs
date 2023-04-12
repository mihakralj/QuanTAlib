namespace QuanTAlib;
using System;

/* <summary>
CMO: Chande Momentum Oscillator
	Chande Momentum Oscillator (also known as CMO indicator) was developed by Tushar S. Chande
	CMO is similar to other momentum oscillators (e.g. RSI or Stochastics). Alike RSI oscillator,
	the CMO values move in the range from -100 to +100 points and its aim is to detect the
	overbought and oversold market conditions. CMO calculates the price momentum on both the up
	days as well as the down days. The CMO calculation is based on non-smoothed price values
	meaning that it can reach its extremes more frequently and the short-time swings are more visible.

Sources:
    https://www.technicalindicators.net/indicators-technical-analysis/144-cmo-chande-momentum-oscillator

</summary> */

public class CMO_Series : Single_TSeries_Indicator {
	private readonly System.Collections.Generic.List<double> _buff_up = new();
	private readonly System.Collections.Generic.List<double> _buff_dn = new();
	private double _plast_value, _last_value;

	public CMO_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN) {
		if (this._data.Count > 0) { base.Add(this._data); }
	}

	public override void Add((DateTime t, double v) TValue, bool update) {
		if (this.Count == 0) { _plast_value = _last_value = TValue.v; }
		if (update) {_last_value = _plast_value;} else {_plast_value = _last_value;}

		Add_Replace_Trim(_buff_up, (TValue.v > _last_value) ? TValue.v-_last_value : 0, _p, update);
		Add_Replace_Trim(_buff_dn, (TValue.v < _last_value) ? _last_value-TValue.v : 0, _p, update);
		_last_value = TValue.v;

		double _cmo_up = 0;
		double _cmo_dn = 0;
		for (int i = 0; i < Math.Min(_buff_up.Count, _buff_dn.Count); i++) {
			_cmo_up += _buff_up[i];
			_cmo_dn += _buff_dn[i];
		}

		double _cmo = 100 * (_cmo_up - _cmo_dn) / (_cmo_up + _cmo_dn);
		if (_cmo_up + _cmo_dn == 0) {_cmo = 0;}
		base.Add((TValue.t, _cmo), update, _NaN);
	}
}