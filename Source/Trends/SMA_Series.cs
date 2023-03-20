namespace QuanTAlib;
using System;

/* <summary>
SMA: Simple Moving Average
    The weights are equally distributed across the period, resulting in a mean() of
    the data within the period

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/simple-moving-average-sma/
    https://stats.stackexchange.com/a/24739

Remark:
    This calc doesn't use LINQ or SUM() or any of (slow) iterative methods. It is not as fast as TA-LIB
    implementation, but it does allow incremental additions of inputs and real-time calculations of SMA()

</summary> */

public class SMA_Series : Single_TSeries_Indicator {
	private double _sum, _oldsum;
	private int _len;

	public SMA_Series(TSeries source, int period = 0, bool useNaN = false) : base(source, period, false) {
		_sum = _oldsum = 0;
		_len = 0;
		if (this._data.Count > 0) { base.Add(this._data); }
	}

	public override void Add((DateTime t, double v) TValue, bool update) {
		if (update) { _sum = _oldsum; }
		else { _oldsum = _sum; _len++; }

		_sum += TValue.v;
		if (_period != 0 && _len > _period) { 
			_sum -= (_data[base.Count - _period - (update ? 1 : 0)].v); 
		}
		double _div = (_period == 0) ? _len : Math.Min(_len, _period);
		base.Add((TValue.t, _sum / _div), update, _NaN);
	}
	public void Reset() {
		_sum = _oldsum = 0;
		_len = 0;
	}
}
