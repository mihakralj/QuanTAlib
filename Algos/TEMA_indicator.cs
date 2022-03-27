using System;
namespace QuantLib;

/** 
TEMA: Triple Exponential Moving Average 
TEMA uses EMA(EMA(EMA())) to calculate less laggy Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/triple-exponential-moving-average-tema/

Remark:
    ema1 = EMA(close, length)
    ema2 = EMA(ema1, length)
    ema3 = EMA(ema2, length)
    TEMA = 3 * (ema1 - ema2) + ema3
**/

public class TEMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _k, _k1m;
    private double _ema1, _lastema1, _lastlastema1;
    private double _ema2, _lastema2, _lastlastema2;
    private double _ema3, _lastema3, _lastlastema3;
    private double _tema;

    public TEMA_Series(TSeries source, int period, bool useNaN = false)
    {
        _p = period;
        _data = source;
        _k = 2.0 / (double)(period + 1);
        _k1m = 1.0 - _k;
        _NaN = useNaN;
        _lastema1 = _lastlastema1 = double.NaN;
        _lastema2 = _lastlastema2 = double.NaN;
        _lastema3 = _lastlastema3 = double.NaN;
        source.Pub += this.Sub;
        if (source.Count > 0) for (int i = 0; i < source.Count; i++) this.Add(source[i], false);

    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) {
            _lastema1 = _lastlastema1;
            _lastema2 = _lastlastema2;
            _lastema3 = _lastlastema3;
        }
        _ema1 = System.Double.IsNaN(_lastema1) ? data.v : data.v * _k + _lastema1 * _k1m;
        _ema2 = System.Double.IsNaN(_lastema2) ? _ema1 : _ema1 * _k + _lastema2 * _k1m;
        _ema3 = System.Double.IsNaN(_lastema3) ? _ema2 : _ema2 * _k + _lastema3 * _k1m;
    
        _tema = 3 * (_ema1 - _ema2) + _ema3;

        _lastlastema1 = _lastema1;
        _lastlastema2 = _lastema2;
        _lastlastema3 = _lastema3;
        _lastema1 = _ema1;
        _lastema2 = _ema2;
        _lastema3 = _ema3;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _tema);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }

    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e) { this.Add(_data[_data.Count - 1], e.update); }

} 