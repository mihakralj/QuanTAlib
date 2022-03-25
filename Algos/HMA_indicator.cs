using System;
namespace QuantLib;

/** 
HMA: Hull Moving Average 
Developed by Alan Hull, an extremely fast and smooth moving average; almost eliminates lag altogether 
and manages to improve smoothing at the same time.

Sources:
    https://alanhull.com/hull-moving-average
    https://school.stockcharts.com/doku.php?id=technical_indicators:hull_moving_average
WMA1 = WMA(n/2) of price
WMA2 = WMA(n) of price
Raw HMA = (2 * WMA1) - WMA2
HMA = WMA(sqrt(n)) of Raw HMA
**/

public class HMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _wma1, _wma2, _hma;
    private System.Collections.Generic.List<double> _buf1 = new();
    private System.Collections.Generic.List<double> _buf2 = new();
    private System.Collections.Generic.List<double> _buf3 = new();
    private System.Collections.Generic.List<double> _weights = new();

    public HMA_Series(TSeries source, int period, bool useNaN = false)
    {
        _p = period;
        _data = source;
        _NaN = useNaN;
        for (int i = 0; i < _p; i++) _weights.Add(i + 1);
        source.Pub += this.Sub;
    }
    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) {
            _buf1[_buf1.Count-1] = data.v;
            _buf2[_buf2.Count-1] = data.v;
        } else {
            _buf1.Add(data.v);
            _buf2.Add(data.v);
        }
        if (_buf1.Count > (int)(Math.Ceiling((double)_p/2))) _buf1.RemoveAt(0);
        if (_buf2.Count > (int)_p) _buf2.RemoveAt(0);

        _wma1 = 0;
        for (int i = 0; i < _buf1.Count; i++) _wma1 += _buf1[i] * _weights[i];
        _wma1 /= (_buf1.Count * (_buf1.Count+1)) * 0.5;

        _wma2 = 0;
        for (int i = 0; i < _buf2.Count; i++) _wma2 += _buf2[i] * _weights[i];
        _wma2 /= (_buf2.Count * (_buf2.Count + 1)) * 0.5;

        if (update) _buf3[_buf3.Count - 1] = 2*_wma1-_wma2; else _buf3.Add(2*_wma1-_wma2);
        if (_buf3.Count > (int)Math.Sqrt(_p)) _buf3.RemoveAt(0);

        _hma = 0;
        for (int i = 0; i < _buf3.Count; i++) _hma += _buf3[i] * _weights[i];
        _hma /= (_buf3.Count * (_buf3.Count+1)) * 0.5;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _hma);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }
    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }
    public void Sub(object source, TSeriesEventArgs e) { this.Add(_data[_data.Count - 1], e.update); }

}