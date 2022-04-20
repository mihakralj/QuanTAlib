namespace QuanTAlib;
using System;

/* <summary>
RMA: wildeR Moving Average
    J. Welles Wilder introduced RMA as an alternative to EMA. RMA's weight (k) is
    set as 1/period, giving less weight to the new data compared to EMA. 

Sources:
    https://archive.org/details/newconceptsintec00wild/page/23/mode/2up
	https://tlc.thinkorswim.com/center/reference/Tech-Indicators/studies-library/V-Z/WildersSmoothing
    https://www.incrediblecharts.com/indicators/wilder_moving_average.php

Issues:
    Pandas-TA library calculates RMA using straight Exponential Weighted Mean:
	pandas.ewm().mean() and returns incorrect first (period) of bars compared to 
	published formula. This implementation passess the validation test in Wilder's book.

</summary> */

public class RMA_Series : Single_TSeries_Indicator
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly double _k, _k1m;
    private double _lastema, _lastlastema;

    public RMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        this._k = 1.0 / (double)(this._p);
        this._k1m = 1.0 - this._k;
        this._lastema = this._lastlastema = double.NaN;
        if (_data.Count > 0) { base.Add(_data); }
    }

    public override void Add((DateTime t, double v) TValue, bool update)
    {
        double _ema = 0;
        if (update) { this._lastema = this._lastlastema; }

        if (this.Count < this._p)
        {
            if (update) { _buffer[_buffer.Count - 1] = TValue.v; }
            else
            {
                _buffer.Add(TValue.v);
            }
            if (_buffer.Count > this._p) { _buffer.RemoveAt(0); }

            for (int i = 0; i < _buffer.Count; i++) { _ema += _buffer[i]; }
            _ema /= this._buffer.Count;
        }
        else
        {
            _ema = TValue.v * _k + _lastema * _k1m;
        }

        this._lastlastema = this._lastema;
        this._lastema = _ema;

        var ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _ema);
        base.Add(ret, update);
    }
}