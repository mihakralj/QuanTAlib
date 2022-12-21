namespace QuanTAlib;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

/* <summary>
DEMA: Double Exponential Moving Average
    DEMA uses EMA(EMA()) to calculate smoother Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/double-exponential-moving-average-dema/

Remark:
    ema1 = EMA(close, length)
    ema2 = EMA(ema1, length)
    DEMA = 2 * ema1 - ema2

</summary> */

public class DEMA_Series : Single_TSeries_Indicator
{
    private readonly System.Collections.Generic.List<double> _buffer1 = new();
    private readonly System.Collections.Generic.List<double> _buffer2 = new();
    private readonly double _k;
	private readonly bool _useSMA;
	private double _lastema1, _lastlastema1;
    private double _lastema2, _lastlastema2;

    public DEMA_Series(TSeries source, int period, bool useNaN = false, bool useSMA = true) : base(source, period, useNaN)
    {
        _k = 2.0 / (_p + 1);
		_useSMA = useSMA;
		if (_data.Count > 0) { base.Add(_data); }
    }

    public override void Add((DateTime t, double v) TValue, bool update)
    {
        if (update)
        {
            _lastema1 = _lastlastema1;
            _lastema2 = _lastlastema2;
        }

        double _ema1, _ema2, _dema;
        if (this.Count < _p && _useSMA)
        {
            Add_Replace_Trim(_buffer1, TValue.v, _p, update);
            _ema1 = 0;
            for (int i=0; i<_buffer1.Count; i++) { _ema1 += _buffer1[i]; }
            _ema1 /= _buffer1.Count;

            Add_Replace_Trim(_buffer2, _ema1, _p, update);
            _ema2 = 0;
            for (int i = 0; i < _buffer2.Count; i++) { _ema2 += _buffer2[i]; }
            _ema2 /= _buffer2.Count;
        }
        else if(this.Count < (2*_p - 1) && _useSMA) // second _p
        {
            _ema1 = (TValue.v - _lastema1) * _k + _lastema1;

            Add_Replace_Trim(_buffer2, _ema1, _p, update);
            _ema2 = 0;
            for (int i = 0; i < _buffer2.Count; i++) { _ema2 += _buffer2[i]; }
            _ema2 /= _buffer2.Count; 
        }
        else // all others
        {
            _ema1 = (TValue.v - _lastema1) * _k + _lastema1;
            _ema2 = (_ema1 - _lastema2) * _k + _lastema2;
            
        }
        _dema = 2*_ema1 - _ema2;

        this._lastlastema1 = this._lastema1;
        this._lastlastema2 = this._lastema2;
        this._lastema1 = _ema1;
        this._lastema2 = _ema2;

        base.Add((TValue.t, _dema), update, _NaN);
    }
}