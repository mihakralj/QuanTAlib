namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
SMA: Simple Moving Average
    The weights are equally distributed across the period, resulting in a mean() of
    the data within the period/

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/simple-moving-average-sma/
    https://stats.stackexchange.com/a/24739

Remark:
    This calc doesn't use LINQ or SUM() or any of (slow) iterative methods. It is not as fast as TA-LIB
    implementation, but it does allow incremental additions of inputs and real-time calculations of SMA()

</summary> */

public class SMA_Series : Single_TSeries_Indicator
{
    public SMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = 0;
        for (int i=0; i<_buffer.Count; i++) { _sma+= _buffer[i]; }
        _sma /= _buffer.Count;

        base.Add((TValue.t, _sma), update, _NaN);
    }
}
