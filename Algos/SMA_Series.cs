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
    private readonly int _p;
    private readonly bool _NaN;
    private readonly TSeries _data;
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly TSeries _mad = new();
     private readonly TSeries _stddev= new();
     private readonly TSeries _mape = new();
     private readonly TSeries  _mse = new();

    public TSeries MAD { get { return _mad; } }
    public TSeries STDDEV { get { return _stddev; } }
    public TSeries MSE { get { return _mse; } }
    public TSeries MAPE { get { return _mape; } }

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

        double _sma_item = 0;
        double _mad_item = 0;
        double _mse_item = 0;
        double _mape_item = 0;
        for (int i = 0; i < this._buffer.Count; i++) {  _sma_item += this._buffer[i]; }
        _sma_item /= this._buffer.Count;

        for (int i = 0; i < this._buffer.Count; i++)
        {
            _mad_item += Math.Abs(this._buffer[i] - _sma_item);
            _mse_item += (this._buffer[i] - _sma_item) * (this._buffer[i] - _sma_item);
            _mape_item += (this._buffer[i] != 0) ? Math.Abs((this._buffer[i] - _sma_item)/this._buffer[i]) : double.PositiveInfinity;
        }
        var _stddev_item = (this._buffer.Count>1)?Math.Sqrt(_mse_item/(this._buffer.Count-1)):0;
        _mad_item /= this._buffer.Count;
        _mse_item /= this._buffer.Count;
        _mape_item /= this._buffer.Count;

        (System.DateTime t, double v) result = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _sma_item);
        (System.DateTime t, double v) stddev = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _stddev_item);
        (System.DateTime t, double v) mad = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _mad_item);
        (System.DateTime t, double v) mse = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _mse_item);
        (System.DateTime t, double v) mape = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _mape_item);

        if (update)
        {
            base[base.Count - 1] = result;
            _stddev[base.Count - 1] = stddev;
            _mad[base.Count - 1] = mad;
            _mse[base.Count - 1] = mse;
            _mape[base.Count - 1] = mape;
        }
        else
        {
            base.Add(result);
            _stddev.Add(stddev);
            _mad.Add(mad);
            _mse.Add(mse);
            _mape.Add(mape);

        }
    }
    public void Add(bool update = false)
    {
        this.Add(this._data[this._data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e) { this.Add(this._data[this._data.Count - 1], e.update); }

}