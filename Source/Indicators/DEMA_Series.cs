namespace QuanTAlib;
using System;

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
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly double _k, _k1m;
    private double _lastema1, _lastlastema1;
    private double _lastema2, _lastlastema2;

    public DEMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        this._k = 2.0 / (this._p + 1);
        this._k1m = 1.0 - this._k;
        if (_data.Count > 0) { base.Add(_data); }
    }

    public override void Add((DateTime t, double v) TValue, bool update)
    {

        if (update)
        {
            this._lastema1 = this._lastlastema1;
            this._lastema2 = this._lastlastema2;
        }

        double _ema1, _ema2;

        if (this.Count < this._p)
        {
            if (update) { _buffer[_buffer.Count - 1] = TValue.v; }
            else
            {
                _buffer.Add(TValue.v);
            }
            if (_buffer.Count > this._p) { _buffer.RemoveAt(0); }

            double _sma = 0;
            for (int i = 0; i < _buffer.Count; i++) { _sma += _buffer[i]; }
            _sma /= this._buffer.Count;
            _ema1 = _ema2 = _sma;

        }
        else
        {
            _ema1 = TValue.v * this._k + this._lastema1 * this._k1m;
            _ema2 = _ema1 * this._k + this._lastema2 * this._k1m;
        }

        double _dema = 2 * _ema1 - _ema2;
        this._lastlastema1 = this._lastema1;
        this._lastlastema2 = this._lastema2;
        this._lastema1 = _ema1;
        this._lastema2 = _ema2;

        var ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _dema);
        base.Add(ret, update);
    }
}
