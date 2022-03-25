using System;
namespace QuantLib;

/** 
ZLEMA: Zero Lag Exponential Moving Average 

The Zero lag exponential moving average (ZLEMA) indicator was created by John Ehlers and Ric Way.

The formula for a given N-Day period and for a given Data series is:
Lag = (Period-1)/2
Ema Data = {Data+(Data-Data(Lag days ago))
ZLEMA = EMA (EmaData,Period)

The idea is do a regular exponential moving average (EMA) calculation but on a de-lagged data instead of 
doing it on the regular data. Data is de-lagged by removing the data from "lag" days ago thus removing 
(or attempting to remove) the cumulative lag effect of the moving average.
**/

public class ZLEMA_Series : TSeries
{
    private int _p, _l;
    private bool _NaN;
    private TSeries _data;
    private double _k, _k1m, _lagdata;
    private double _ema, _lastema, _lastlastema;

    public ZLEMA_Series(TSeries source, int period, bool useNaN = false)
    {
        _p = period;
        _l = (int)Math.Round((_p - 1) * 0.5);
        _data = source;
        _k = 2.0 / (double)(period + 1);
        _k1m = 1.0 - _k;
        _NaN = useNaN;
        _lastema = _lastlastema = double.NaN;
        source.Pub += this.Sub;
    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) _lastema = _lastlastema;

        _lagdata = _data[_data.Count - 1].v + (_data[_data.Count - 1].v - _data[Math.Max(_data.Count - 1 - _l, 0)].v);

        _ema = System.Double.IsNaN(_lastema) ? _lagdata : _lagdata * _k + _lastema * _k1m;
        _lastlastema = _lastema;
        _lastema = _ema;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _ema);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }

    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }
    public void Sub(object source, TSeriesEventArgs e) { this.Add(_data[_data.Count - 1], e.update); }

}