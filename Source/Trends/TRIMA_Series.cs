namespace QuanTAlib;
using System;

/* <summary>
TRIMA: Triangular Moving Average
    A weighted moving average where the shape of the weights are triangular and the greatest
    weight is in the middle of the period,

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/triangular-moving-average-trima/

Remark:
    trima = sma(sma(signal, n/2), n/2)

</summary> */

public class TRIMA_Series : Single_TSeries_Indicator
{
    private readonly System.Collections.Generic.List<double> _buffer1 = new();
    private readonly System.Collections.Generic.List<double> _buffer2 = new();
    private readonly int _p1a, _p1b;
    
    public TRIMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        _p1a = (int) Math.Floor((period * 0.5) + 1);
        _p1b = (int) Math.Ceiling(0.5 * period);
        if (base._data.Count > 0) { base.Add(base._data); }
    }

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { _buffer1[_buffer1.Count - 1] = TValue.v; } else { _buffer1.Add(TValue.v); }
        if (_buffer1.Count > this._p1b && this._p1b != 0) { _buffer1.RemoveAt(0); }
        
        double _sma1 = 0;
        for (int i = 0; i < _buffer1.Count; i++) { _sma1 += _buffer1[i]; }
        _sma1 /= this._buffer1.Count;

        if (update) { _buffer2[_buffer2.Count - 1] = _sma1; } else { _buffer2.Add(_sma1); }
        if (_buffer2.Count > this._p1a && this._p1a != 0) { _buffer2.RemoveAt(0); }

        double _trima = 0;        
        for (int i = 0; i < _buffer2.Count; i++) { _trima += _buffer2[i]; }
        _trima /= this._buffer2.Count;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _trima);
        base.Add(result, update);
    }
}