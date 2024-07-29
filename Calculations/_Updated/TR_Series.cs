namespace QuanTAlib;
using System;
using System.Collections.Generic;

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

public class TR_Series : TSeries
{
    protected readonly TBars _data;
    private double _cm1, _cm1_o;

    //core constructors
    public TR_Series()
    {
        Name = $"TR()";
        _cm1 = _cm1_o = double.NaN;
    }
    public TR_Series(TBars source)
    {
        _data = source;
        Name = $"TR({(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _cm1 = _cm1_o = double.NaN;
        _data.Pub += Sub;
        Add(data: _data);
    }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false)
    {

        if (update)
        {
            _cm1 = _cm1_o;
        }
        else
        {
            _cm1_o = _cm1;
        }

        if (_cm1 is double.NaN)
        {
            _cm1 = TBar.c;
        }

        double d1 = Math.Abs(TBar.h - TBar.l);
        double d2 = Math.Abs(_cm1 - TBar.h);
        double d3 = Math.Abs(_cm1 - TBar.l);
        _cm1 = TBar.c;
        var ret = (TBar.t, Math.Max(d1, Math.Max(d2, d3)));
        return base.Add(ret, update);

    }

    public new void Add(TBars data)
    {
        foreach (var item in data) { Add(item, false); }
    }
    public (DateTime t, double v) Add(bool update)
    {
        return this.Add(TBar: _data.Last, update: update);
    }
    public (DateTime t, double v) Add()
    {
        return Add(TBar: _data.Last, update: false);
    }
    private new void Sub(object source, TSeriesEventArgs e)
    {
        Add(TBar: _data.Last, update: e.update);
    }

    //reset calculation
    public override void Reset()
    {
        _cm1 = _cm1_o = double.NaN;
    }
}