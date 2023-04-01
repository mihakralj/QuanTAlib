namespace QuanTAlib;
using System;

/* <summary>
ZL: Zero Lag
    Data is de-lagged by removing the data from “lag” days ago, thus removing
    (or attempting to) the cumulative effect of the moving average.

Calculation:
    Lag = (Period-1)/2
    ZL = Data + (Data - Data(Lag days ago) )

Sources:
     https://mudrex.com/blog/zero-lag-ema-trading-strategy/

</summary> */

public class ZL_Series : Single_TSeries_Indicator
{
    public ZL_Series(TSeries source, int period, bool useNaN = false) : base(source, period:period, useNaN:useNaN) {
        if (this._data.Count > 0) { base.Add(this._data); }
    }

    public override void Add((DateTime t, double v) TValue, bool update)
    {
        int _lag = (int)((_p-1) * 0.5);
        _lag = (this.Count-_lag < 0) ? 0 : this.Count-_lag;

        double _zl = TValue.v + (TValue.v - _data[_lag].v);

        var ret = (TValue.t, (base.Count==0 && base._NaN) ? double.NaN : _zl );
        base.Add(ret, update);
    }
}