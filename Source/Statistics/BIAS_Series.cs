namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
BIAS: Rate of change between the source and a moving average.
    Bias is a statistical term which means a systematic deviation from the actual value.

BIAS = (close - SMA) / SMA
       = (close / SMA) - 1

Sources:
	https://en.wikipedia.org/wiki/Bias_of_an_estimator

</summary> */

public class BIAS_Series : Single_TSeries_Indicator
{
    public BIAS_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);

        double _sma = _buffer.Average();
        double _bias = (_buffer[_buffer.Count - 1] / ((_sma != 0) ? _sma : 1)) - 1;

        base.Add((TValue.t, _bias), update, _NaN);
    }
}
