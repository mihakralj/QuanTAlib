namespace QuanTAlib;
using System;

/* <summary>
SUM: Cumulative Sum (aka Running Total)
    SUM across a period provides a rolling sum of all values across the period.
    If SUM values would be divided with period, the output would be SMA()

Sources:
    https://en.wikipedia.org/wiki/CUSUM

</summary> */

public class SUM_Series : Single_TSeries_Indicator
{
    public SUM_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { _buffer[_buffer.Count - 1] = TValue.v; }
        else { _buffer.Add(TValue.v); }
        if (_buffer.Count > this._p && this._p != 0) { _buffer.RemoveAt(0); }

        double _sum = 0;
        for (int i = 0; i < _buffer.Count; i++) { _sum += _buffer[i]; }

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _sum);

        base.Add(result, update);
    }
}
