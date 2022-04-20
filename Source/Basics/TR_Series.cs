namespace QuanTAlib;
using System;

/* <summary>
TR: True Range
    True Range was introduced by J. Welles Wilder in his book New Concepts in Technical Trading Systems.
    It measures the daily range plus any gap from the closing price of the preceding day.

Calculation:
    d1 = ABS(High - Low)
    d2 = ABS(High - Previous close)
    d3 = ABS(Previous close - Low)
    TR = MAX(d1,d2,d3)

Sources:
     https://www.macroption.com/true-range/

</summary> */

public class TR_Series : Single_TBars_Indicator
{
    private double _cm1 = double.NaN;
    public TR_Series(TBars source, bool useNaN = false) : base(source, period:0, useNaN:useNaN) {
        if (this._bars.Count > 0) { base.Add(this._bars); }
    }

    public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update)
    {
        if (_cm1 is double.NaN) { _cm1 = TBar.c; }

        double d1 = Math.Abs(TBar.h - TBar.l);
        double d2 = Math.Abs(_cm1 - TBar.h);
        double d3 = Math.Abs(_cm1 - TBar.l);
        var ret = (TBar.t, (base.Count==0 && base._NaN) ? double.NaN : Math.Max(d1,Math.Max(d2,d3)) );
        base.Add(ret, update);
        _cm1 = TBar.c;
    }
}