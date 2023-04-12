namespace QuanTAlib;
using System;

/* <summary>
DWMA: Double Weighted Moving Average
    The weights are decreasing over the period with p^2 decay
		and the most recent data has the heaviest weight.

</summary> */

public class DWMA_Series : Single_TSeries_Indicator {
	private readonly System.Collections.Generic.List<double> _buffer1 = new();
	private readonly System.Collections.Generic.List<double> _weights = new();
	public DWMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN) {
		for (int i = 0; i < this._p; i++) {
			double _weight = (i + 1) * (i + 1);
			this._weights.Add(_weight);
		}
		if (base._data.Count > 0) { base.Add(base._data); }
	}

	public override void Add((System.DateTime t, double v) TValue, bool update) {
		Add_Replace_Trim(_buffer1, TValue.v, _p, update);
		double _wma1 = 0, _wsum = 0;
		for (int i = 0; i < _buffer1.Count; i++) {
			_wma1 += _buffer1[i] * _weights[i];
			_wsum += _weights[i];
		}
		_wma1 /= _wsum;

		base.Add((TValue.t, _wma1), update, _NaN);
	}
}