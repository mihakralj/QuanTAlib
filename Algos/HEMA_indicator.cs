using System;
namespace QuantLib;

/** 
HEMA: Hull-EMA Moving Average 
Modified HUll Moving Average; instead of using WMA (Weighted MA) for a calculation, HEMA uses EMA for Hull's
formula:

EMA1 = EMA(n/2) of price - where k = 4/(n/2 +1)
EMA2 = EMA(n) of price - where k = 3/(n+1)
Raw HMA = (2 * EMA1) - EMA2 
EMA3 = EMA(sqrt(n)) of Raw HMA - where k = 2/(sqrt(n)+1)
**/

public class HEMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _k1, _k2, _k3;
    private double _ema1, _lastema1, _lastlastema1;
    private double _ema2, _lastema2, _lastlastema2;
    private double _ema3, _lastema3, _lastlastema3;

    public HEMA_Series(TSeries source, int period, bool useNaN = true)
    {
        _p = period;
        _data = source;
        _k1 = 4 / (double)((period*0.5)+1);
        _k2 = 3 / (double)(period+1);
        _k3 = 2 / (double)(Math.Sqrt(period)+1);

        _NaN = useNaN;
        _lastema1 = _lastlastema1 = double.NaN;
        _lastema2 = _lastlastema2 = double.NaN;
        _lastema3 = _lastlastema3 = double.NaN;
    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) {
            _lastema1 = _lastlastema1;
            _lastema2 = _lastlastema2;
            _lastema3 = _lastlastema3;
        }
        _ema1 = System.Double.IsNaN(_lastema1) ? data.v : data.v * _k1 + _lastema1 * (1 - _k1);
        _ema2 = System.Double.IsNaN(_lastema2) ? data.v : data.v * _k2 + _lastema2 * (1 - _k2);

        double _rawhema = (2 * _ema1) - _ema2;
        _ema3 =  System.Double.IsNaN(_lastema3) ? _rawhema : _rawhema * _k3 + _lastema3 * (1 - _k3);

        _lastlastema1 = _lastema1;
        _lastlastema2 = _lastema2;
        _lastlastema3 = _lastema3;
        _lastema1 = _ema1;
        _lastema2 = _ema2;
        _lastema3 = _ema3;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _ema3);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }

    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }
} 