namespace QuanTAlib;
using System;

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
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { this._buffer.Add(TValue.v); }
        if (this._buffer.Count > this._p && this._p != 0) { this._buffer.RemoveAt(0); }

        double _sma = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _sma += this._buffer[i]; }
        _sma /= this._buffer.Count;
        double _bias = (this._buffer[this._buffer.Count - 1] / ((_sma != 0) ? _sma : 1)) - 1;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _bias);

        base.Add(result, update);
    }
}
