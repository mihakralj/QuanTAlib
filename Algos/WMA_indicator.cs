using System;
namespace QuantLib;

/** 
WMA: (linearly) Weighted Moving Average 
The weights are linearly decreasing over the period and the most recent data has the heaviest weight.

Sources:
    https://corporatefinanceinstitute.com/resources/knowledge/trading-investing/weighted-moving-average-wma/
    https://www.technicalindicators.net/indicators-technical-analysis/83-moving-averages-simple-exponential-weighted
**/

public class WMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _wma;
    private System.Collections.Generic.List<double> _buffer = new();
    private System.Collections.Generic.List<double> _weights = new();

    public WMA_Series(TSeries source, int period, bool useNaN = false)
    {
        _p = period;
        _data = source;
        _NaN = useNaN;
        for (int i = 0; i < _p; i++) _weights.Add(i + 1);
        source.Pub += this.Sub;
        if (source.Count > 0) for (int i = 0; i < source.Count; i++) this.Add(source[i], false);

    }
    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) _buffer[_buffer.Count - 1] = data.v; else _buffer.Add(data.v);
        if (_buffer.Count > _p) _buffer.RemoveAt(0);

        _wma = 0;
        for (int i = 0; i < _buffer.Count; i++) _wma += _buffer[i] * _weights[i];
        _wma /= (_buffer.Count * (_buffer.Count + 1)) * 0.5;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _wma);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }
    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e) { this.Add(_data[_data.Count - 1], e.update); }

}