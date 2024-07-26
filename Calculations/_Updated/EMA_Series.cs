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

public class EMA_Series : TSeries {
	private double _k;
	private double _lastema, _oldema;
	private double _sum, _oldsum;
	private int _len;
	private readonly bool _useSMA;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

//core constructors

	public EMA_Series(int period, bool useNaN, bool useSMA) {
		_period = period;
		_NaN = useNaN;
		_useSMA = useSMA;
		Name = $"EMA({period})";
		_k = 2.0 / (_period + 1);
		_len = 0;
		_sum = _oldsum = _lastema = _oldema = 0;
	}	
	public EMA_Series(TSeries source, int period, bool useNaN, bool useSMA) : this(period, useNaN, useSMA) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public EMA_Series() : this(0, false, true) {}
	public EMA_Series(int period) : this(period, false, true) {}
	public EMA_Series(TBars source) : this(source.Close, 0, false) {}
	public EMA_Series(TBars source, int period) : this(source.Close, period, false) {}
	public EMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) {}
	public EMA_Series(TSeries source, int period) : this(source, period, false, true) {}
	public EMA_Series(TSeries source, int period, bool useNaN) : this(source, period, useNaN, true) {}


	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		if (update) {
			_lastema = _oldema;
			_sum = _oldsum;
		}
		else {
			_oldema = _lastema;
			_oldsum = _sum;
			_len++;
		}

		double _ema = 0;
		if (_period == 0) {
			_k = 2.0 / (_len + 1);
		}

		if (Count == 0) {
			_ema = _sum = TValue.v;
		}
		else if (_len <= _period && _useSMA && _period != 0) {
			_sum += TValue.v;
			if (_period != 0 && _len > _period) {
				_sum -= _data[Count - _period - (update ? 1 : 0)].v;
			}

			_ema = _sum / Math.Min(_len, _period);
		}
		else {
			_ema = _k * (TValue.v - _lastema) + _lastema;
		}

		_lastema = double.IsNaN(_ema) ? _lastema : _ema;

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _ema);
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
		_sum = _oldsum = _lastema = _oldema = 0;
		_len = 0;
	}
}