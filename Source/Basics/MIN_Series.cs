namespace QuanTAlib;
using System;

/* <summary>
MIN - Minimum value in the given period in the series.
    If period = 0 => period = full length of the series
</summary> */

public class MIN_Series : Single_TSeries_Indicator
{
    public MIN_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) d, bool update)
    {
        if (update) { this._buffer[this._buffer.Count - 1] = d.v; }
        else { this._buffer.Add(d.v); }
        if (this._buffer.Count > this._p && this._p != 0) { this._buffer.RemoveAt(0); }

        double _min = d.v;
        for (int i = 0; i < this._buffer.Count; i++)
        { _min = (this._buffer[i] < _min) ? this._buffer[i] : _min; }

        var result = (d.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _min);

        base.Add(result, update);
    }
}