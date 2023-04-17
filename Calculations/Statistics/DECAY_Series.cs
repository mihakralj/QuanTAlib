namespace QuanTAlib;
using System;
using System.Collections.Generic;


/* <summary>
DECAY: 
  Linear decay can be modeled by a straight line with a negative slope of 1/period.
	The value decreases in a straight line from the last maximum to 0.
	Decay = Last Max - distance/period

	Exponential decay  is modeled as an exponential curve with diminishing factor of
	1-1/p

</summary> */

public class DECAY_Series : Single_TSeries_Indicator {
	private readonly bool _exp;
	private double _pdecay, _ppdecay;
	private readonly double _dfactor;

	public DECAY_Series(TSeries source, int period = 10, bool exponential= false, bool useNaN = false) : base(source, period, false) {
		_exp = exponential;
		_dfactor = (_exp)? 1.0 - 1.0 / (double)_p : 1/(double)_p;
		_pdecay = _ppdecay = 0;
		if (source.Count > 0) { base.Add(this._data); }
	}

	public override void Add((DateTime t, double v) TValue, bool update) {
		if (update) { _pdecay = _ppdecay; }
		else { _ppdecay = _pdecay; }

		if (this.Count == 0) { _pdecay = TValue.v; }
		double _decay = Math.Max(TValue.v, Math.Max((_exp)?_pdecay*_dfactor:_pdecay-_dfactor, 0));
		_pdecay = _decay;

		base.Add((TValue.t, _decay), update, _NaN);
	}
}
