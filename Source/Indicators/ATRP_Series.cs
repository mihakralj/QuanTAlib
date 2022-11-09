namespace QuanTAlib;
using System;

/* <summary>
ATRP: Average True Range Percent
    Average True Range Percent is (ATR/Close Price)*100. 
    This normalizes so it can be compared to other stocks.

Sources:
    https://www.fidelity.com/learning-center/trading-investing/technical-analysis/technical-indicator-guide/atrp

</summary> */

public class ATRP_Series : Single_TBars_Indicator
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly double _k, _k1m;
    private double _lastema, _lastlastema, _lastcm1;
    private double _cm1 = double.NaN;

    public ATRP_Series(TBars source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        this._k = 1.0 / (double)(this._p);
        this._k1m = 1.0 - this._k;
        this._lastema = this._lastlastema = double.NaN;
        if (_bars.Count > 0) { base.Add(_bars); }
    }

    public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update)
    {
        if (update) { 
            this._lastema = this._lastlastema; 
            this._cm1 = this._lastcm1;
        }

        if (_cm1 is double.NaN) { _cm1 = TBar.c; }
        double d1 = Math.Abs(TBar.h - TBar.l);
        double d2 = Math.Abs(_cm1 - TBar.h);
        double d3 = Math.Abs(_cm1 - TBar.l);
        (DateTime t, double v)d = (TBar.t, Math.Max(d1,Math.Max(d2,d3))); //TR value for RMA below
        _lastcm1 = _cm1;
        _cm1 = TBar.c;

        double _ema = 0;
        if (this.Count < this._p)
        {
            if (update) { _buffer[_buffer.Count - 1] = d.v; }
                else { _buffer.Add(d.v); }
            if (_buffer.Count > this._p) { _buffer.RemoveAt(0); }
            for (int i = 0; i < _buffer.Count; i++) { _ema += _buffer[i]; }
            _ema /= this._buffer.Count;
        }
        else { _ema = d.v * _k + _lastema * _k1m; }

        this._lastlastema = this._lastema;
        this._lastema = _ema;

        double _atrp = 100 * (_ema / TBar.c);

        var ret = (d.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _atrp);
        base.Add(ret, update);
    }
}