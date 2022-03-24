using System;
namespace QuantLib;

/** 
EMA: Exponential Moving Average 

EMA needs very short history buffer and calculates the EMA value using just the previous EMA value.
The weight of the new datapoint (k) is k = 2 / (period-1)
Sources:
    https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
    https://www.investopedia.com/ask/answers/122314/what-exponential-moving-average-ema-formula-and-how-ema-calculated.asp
    https://blog.fugue88.ws/archives/2017-01/The-correct-way-to-start-an-Exponential-Moving-Average-EMA
Issues:
    There is no consensus what the first EMA value should be - a zero, a first datapoint, or an average of the initial Period bars.
    All three starting methods converge within 15+ bars to the same moving average - and the simplest method is to use a first datapoint
    That's what this algo is using, and expects at least *Period* of history (warm-up) datapoints before it provides reliable results.
**/

public class EMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _k, _k1m;
    private double _ema, _lastema, _lastlastema;

    public EMA_Series(TSeries source, int period, bool useNaN = true)
    {
        _p = period;
        _data = source;
        _k = 2.0 / (double)(period + 1);
        _k1m = 1.0 - _k;
        _NaN = useNaN;
        _lastema = _lastlastema = double.NaN;
    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) _lastema = _lastlastema;
        _ema = System.Double.IsNaN(_lastema) ? data.v : data.v * _k + _lastema * _k1m;
        _lastlastema = _lastema;
        _lastema = _ema;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _ema);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }

    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }

} 