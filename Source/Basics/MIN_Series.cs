namespace QuanTAlib;
using System;
using System.Linq;

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

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);

        double _min = _buffer.Min();
        base.Add((TValue.t, _min), update, _NaN);
    }
}