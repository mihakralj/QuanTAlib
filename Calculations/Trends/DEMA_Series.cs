namespace QuanTAlib;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

/* <summary>
DEMA: Double Exponential Moving Average
    DEMA uses EMA(EMA()) to calculate smoother Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/double-exponential-moving-average-dema/

Remark:
    ema1 = EMA(close, length)
    ema2 = EMA(ema1, length)
    DEMA = 2 * ema1 - ema2

</summary> */

public class DEMA_Series : Single_TSeries_Indicator
{
  private readonly double _k;
	private int _len;
	private readonly bool _useSMA;
  private double _sum, _lastsum, _lastlastsum;
	private double _lastema1, _lastlastema1;
  private double _lastema2, _lastlastema2;

    public DEMA_Series(TSeries source, int period, bool useNaN = false, bool useSMA = true) : base(source, period, useNaN)
    {
    _k = 2.0 / (_p + 1);
		_len = 0;
		_useSMA = useSMA;
    _sum = _lastema1 = _lastema2 =0;
		if (_data.Count > 0) { base.Add(_data); }
    }

    public override void Add((DateTime t, double v) TValue, bool update)
    {
    if (update) {
      _lastsum = _lastlastsum;
      _lastema1 = _lastlastema1;
      _lastema2 = _lastlastema2;
    }
    else {
      _lastlastsum = _lastsum;
			_lastlastema1 = _lastema1;
			_lastlastema2 = _lastema2;
      _len++;
		}

    double _ema1, _ema2, _dema;
		if (this.Count == 0) {
			_ema1 = _ema2 = _sum = TValue.v;
		}
		else if (_len <= _period && _useSMA && _period != 0) {
			_sum += TValue.v;
			if (_period != 0 && _len > _period) {
				_sum -= (_data[base.Count - _period - (update ? 1 : 0)].v);
			}
			_ema1 = _sum / Math.Min(_len, _period);
			_ema2 = _ema1;
    }
    else {
      _ema1 = (TValue.v - _lastema1) * _k + _lastema1;
      _ema2 = (_ema1 - _lastema2) * _k + _lastema2;
    }
    _dema = 2*_ema1 - _ema2;

	  _lastema1 = Double.IsNaN(_ema1)?_lastema1:_ema1;
    _lastema2 = Double.IsNaN(_ema2)?_lastema2:_ema2;

		base.Add((TValue.t, _dema), update, _NaN);
    }
}