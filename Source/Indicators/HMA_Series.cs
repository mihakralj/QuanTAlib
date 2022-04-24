namespace QuanTAlib;
using System;

/* <summary>
HMA: Hull Moving Average
    Developed by Alan Hull, an extremely fast and smooth moving average; almost
    eliminates lag altogether and manages to improve smoothing at the same time.

Sources:
    https://alanhull.com/hull-moving-average
    https://school.stockcharts.com/doku.php?id=technical_indicators:hull_moving_average

WMA1 = WMA(n/2) of price
WMA2 = WMA(n) of price
Raw HMA = (2 * WMA1) - WMA2
HMA = WMA(sqrt(n)) of Raw HMA

</summary> */

public class HMA_Series : TSeries
{
    private readonly int _p;
    private readonly bool _NaN;
    private readonly TSeries _data;
    private double _wma1, _wma2;
    private readonly System.Collections.Generic.List<double> _buf1 = new();
    private readonly System.Collections.Generic.List<double> _buf2 = new();
    private readonly System.Collections.Generic.List<double> _buf3 = new();
    private readonly System.Collections.Generic.List<double> _weights = new();

    public HMA_Series(TSeries source, int period, bool useNaN = false)
    {
        this._p = period;
        this._data = source;
        this._NaN = useNaN;
        for (int i = 0; i < this._p; i++)
        {
            this._weights.Add(i + 1);
        }

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
            this._buf1[this._buf1.Count - 1] = data.v;
            this._buf2[this._buf2.Count - 1] = data.v;
        }
        else
        {
            this._buf1.Add(data.v);
            this._buf2.Add(data.v);
        }
        if (this._buf1.Count > (int)((double)this._p / 2))
        {
            this._buf1.RemoveAt(0);
        }
        if (this._buf2.Count > this._p)
        {
            this._buf2.RemoveAt(0);
        }

        this._wma1 = 0;
        for (int i = 0; i < this._buf1.Count; i++)
        {
            this._wma1 += this._buf1[i] * this._weights[i];
        }

        this._wma1 /= (this._buf1.Count * (this._buf1.Count + 1)) * 0.5;

        this._wma2 = 0;
        for (int i = 0; i < this._buf2.Count; i++)
        {
            this._wma2 += this._buf2[i] * this._weights[i];
        }

        this._wma2 /= (this._buf2.Count * (this._buf2.Count + 1)) * 0.5;

        if (update)
        {
            this._buf3[this._buf3.Count - 1] = 2 * this._wma1 - this._wma2;
        }
        else
        {
            this._buf3.Add(2 * this._wma1 - this._wma2);
        }
        if (this._buf3.Count > (int)Math.Sqrt(this._p))
        {
            this._buf3.RemoveAt(0);
        }

        double _hma = 0;
        for (int i = 0; i < this._buf3.Count; i++)
        {
            _hma += this._buf3[i] * this._weights[i];
        }

        _hma /= (this._buf3.Count * (this._buf3.Count + 1)) * 0.5;

        (System.DateTime t, double v) result =
            (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _hma);
        base.Add(result, update);
    }
    public void Add(bool update = false)
    {
        this.Add(this._data[this._data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e)
    {
        this.Add(this._data[this._data.Count - 1], e.update);
    }
}
