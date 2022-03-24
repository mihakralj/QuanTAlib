using System;
namespace QuantLib;

/** 
DEMA: Double Exponential Moving Average 
DEMA uses EMA(EMA()) to calculate smoother Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/double-exponential-moving-average-dema/

Remark:
    ema1 = EMA(close, length)
    ema2 = EMA(ema1, length)
    DEMA = 2 * ema1 - ema2
**/

public class DEMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _k, _k1m;
    private double _ema1, _lastema1, _lastlastema1;
    private double _ema2, _lastema2, _lastlastema2;
    private double _dema;

    public DEMA_Series(TSeries source, int period, bool useNaN = true)
    {
        _p = period;
        _data = source;
        _k = 2.0 / (double)(period + 1);
        _k1m = 1.0 - _k;
        _NaN = useNaN;
        _lastema1 = _lastlastema1 = double.NaN;
        _lastema2 = _lastlastema2 = double.NaN;
    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) {
            _lastema1 = _lastlastema1;
            _lastema2 = _lastlastema2;
        }
        _ema1 = System.Double.IsNaN(_lastema1) ? data.v : data.v * _k + _lastema1 * _k1m;
        _ema2 = System.Double.IsNaN(_lastema2) ? _ema1 : _ema1 * _k + _lastema2 * _k1m;
        _dema = 2 * _ema1 - _ema2;

        _lastlastema1 = _lastema1;
        _lastlastema2 = _lastema2;
        _lastema1 = _ema1;
        _lastema2 = _ema2;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _dema);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }

    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }

} 