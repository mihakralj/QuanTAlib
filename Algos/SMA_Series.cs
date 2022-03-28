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
    private readonly int _p;
    private readonly bool _NaN;
    private readonly TSeries _data;
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public SMA_Series(TSeries source, int period, bool useNaN = false)
    {
        this._p = period;
        this._data = source;
        this._NaN = useNaN;
        source.Pub += this.Sub;
        if (source.Count > 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                this.Add(source[i], false);
            }
        }
    }
    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update)
        {
            this._buffer[this._buffer.Count - 1] = data.v;
        }
        else
        {
            this._buffer.Add(data.v);
        }

        if (this._buffer.Count > this._p)
        {
            this._buffer.RemoveAt(0);
        }

        double _sma = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _sma += this._buffer[i]; }
        _sma /= this._buffer.Count;

        (System.DateTime t, double v) result = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _sma);
        if (update)
        {
            base[base.Count - 1] = result;
        }
        else
        {
            base.Add(result);
        }
    }
    public void Add(bool update = false)
    {
        this.Add(this._data[this._data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e) { this.Add(this._data[this._data.Count - 1], e.update); }

}