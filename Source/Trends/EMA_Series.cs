namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
EMA: Exponential Moving Average
    EMA needs very short history buffer and calculates the EMA value using just the
    previous EMA value. The weight of the new datapoint (k) is k = 2 / (period-1)

Sources:
    https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
    https://www.investopedia.com/ask/answers/122314/what-exponential-moving-average-ema-formula-and-how-ema-calculated.asp
    https://blog.fugue88.ws/archives/2017-01/The-correct-way-to-start-an-Exponential-Moving-Average-EMA

Issues:
    There is no consensus what the first EMA value should be - a zero, a first
    datapoint, or an average of the initial Period bars. All three starting methods
    converge within 20+ bars to the same moving average. Most implementations (including this one)
    use SMA() for the first Period bars as a seeding value for EMA.

</summary> */

public class EMA_Series : Single_TSeries_Indicator {
	private double _k;
	private double _lastema, _lastlastema;
	private double _sum, _oldsum;
	private int _len;
	private readonly bool _useSMA;

	public EMA_Series(TSeries source, int period, bool useNaN = false, bool useSMA = true) : base(source, period, useNaN) {
		_k = 2.0 / (_p + 1);
		_sum = _oldsum = _lastema = _lastlastema = 0;
		_len = 0;
		_useSMA = useSMA;
		if (this._data.Count > 0) { base.Add(this._data); }
	}

	public override void Add((DateTime t, double v) TValue, bool update) {

		if (update) { _lastema = _lastlastema; _sum = _oldsum; }
		else { _lastlastema = _lastema; _oldsum = _sum; _len++; }

		double _ema = 0;
		// when period = 0, create cumulative/additive series where _k is progressively larger
		if (_period == 0) { _k = 2.0 / (_len + 1); }

		// the first value of the series
		if (this.Count == 0) {
			_ema = _sum = TValue.v;
		}
		// if SMA is used for seeding, calculate SMA within period
		else if (_len <= _period && _useSMA && _period != 0) {
			_sum += TValue.v;
			if (_period != 0 && _len > _period) {
				_sum -= (_data[base.Count - _period - (update ? 1 : 0)].v);
			}
			_ema = _sum / Math.Min(_len, _period);
		}
		// calculate EMA out from last EMA and factor k
		else {
			_ema = _k * (TValue.v - _lastema) + _lastema;
		}
		_lastema = _ema;

		base.Add((TValue.t, _ema), update, _NaN);
	}
	public void Reset() {
		_sum = _oldsum = _lastema = _lastlastema = 0;
		_len =  0;
	}
}