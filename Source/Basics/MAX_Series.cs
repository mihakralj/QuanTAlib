namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
MAX - Maximum value in the given period in the series.
    If period = 0 => period = full length of the series
</summary> */

public class MAX_Series : Single_TSeries_Indicator
{
    public MAX_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _max = _buffer.Max();

        base.Add((TValue.t, _max), update, _NaN);
    }
}