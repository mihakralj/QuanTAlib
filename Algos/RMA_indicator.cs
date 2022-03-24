using System;
namespace QuantLib;

/** 
RMA: wildeR Moving Average 
J. Welles Wilder introduced RMA as an alternative to EMA. RMA's weight (k) is set as 1/period, giving
less weight to the new data compared to EMA.
Sources:
    https://tlc.thinkorswim.com/center/reference/Tech-Indicators/studies-library/V-Z/WildersSmoothing
    https://www.incrediblecharts.com/indicators/wilder_moving_average.php
Issues:
    Pandas-TA library calculates RMA using Exponential Weighted Mean: pandas.ewm().mean() and 
    returns incorrect first bar compared to published RMA formula.
**/

public class RMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _k, _k1m;
    private double _ema, _lastema, _lastlastema;

    public RMA_Series(TSeries source, int period, bool useNaN = true)
    {
        _p = period;
        _data = source;
        _k = 1.0 / (double)period;
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