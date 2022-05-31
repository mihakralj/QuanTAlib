namespace QuanTAlib;
using System;

/* <summary>
ALMA: Arnaud Legoux Moving Average
    The ALMA moving average uses the curve of the Normal (Gauss) distribution, which
    can be shifted from 0 to 1. This allows regulating the smoothness and high
    sensitivity of the indicator. Sigma is another parameter that is responsible for
    the shape of the curve coefficients. This moving average reduces lag of the data
    in conjunction with smoothing to reduce noise.


Sources:
    https://phemex.com/academy/what-is-arnaud-legoux-moving-averages
    https://www.prorealcode.com/prorealtime-indicators/alma-arnaud-legoux-moving-average/

</summary> */

public class ALMA_Series : Single_TSeries_Indicator
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly double[] _weight;
    private double _norm;
    private readonly double _offset, _sigma;

    public ALMA_Series(TSeries source, int period, double offset = 0.85, double sigma = 6.0, bool useNaN = false)
        : base(source, period, useNaN)
    {
        _offset = offset;
        _sigma = sigma;
        _weight = new double[period];

        if (this._data.Count > 0) { base.Add(this._data); }
    }

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { this._buffer.Add(TValue.v); }
        if (this._buffer.Count > this._p) { this._buffer.RemoveAt(0); }

        if (this._buffer.Count <= _p) { calc_weights(); }

        double _weightedSum = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _weightedSum += _weight[i] * _buffer[i]; }
        double _alma = _weightedSum / _norm;

        var ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _alma);
        base.Add(ret, update);
    }

    private void calc_weights()
    {
        int _len = this._buffer.Count;
        _norm = 0;
        double _m = _offset * (_len - 1);
        double _s = _len / _sigma;
        for (int i = 0; i < _len; i++)
        {
            double _wt = Math.Exp(-((i - _m) * (i - _m)) / (2 * _s * _s));
            _weight[i] = _wt;
            _norm += _wt;
        }
    }

}


