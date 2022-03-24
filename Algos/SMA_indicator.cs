using System;
namespace QuantLib;

/** 
SMA: Simple Moving Average 
The weights are equally distributed across the period, resulting in a mean() of the data within the period/

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/simple-moving-average-sma/
    https://stats.stackexchange.com/a/24739

Remark:
    This calc doesn't use LINQ or SUM() or any of iterative methods. 
**/

public class SMA_Series : TSeries
{
    private int _p;
    private bool _NaN;
    private TSeries _data;
    private double _sma;
    private System.Collections.Generic.List<double> _buffer = new();

    public SMA_Series(TSeries source, int period, bool useNaN = true)
    {
        _p = period;
        _data = source;
        _NaN = useNaN;
    }
    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) _buffer[_buffer.Count - 1] = data.v; else _buffer.Add(data.v);
        if (_buffer.Count > _p) _buffer.RemoveAt(0);

        _sma = 0;
        for (int i = 0; i < _buffer.Count; i++) _sma += _buffer[i];
        _sma /= _buffer.Count;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : _sma);
        if (update) base[base.Count - 1] = result; else base.Add(result);
    }
    public void Add(bool update = false)
    {
        this.Add(_data[_data.Count - 1], update);
    }

}