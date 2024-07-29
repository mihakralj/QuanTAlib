namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
ADL: Chaikin Accumulation/Distribution Line
    ADL is a volume-based indicator that measures the cumulative Money Flow Volume:

    1. Money Flow Multiplier = [(Close  -  Low) - (High - Close)] /(High - Low)
    2. Money Flow Volume = Money Flow Multiplier x Volume for the Period
    3. ADL = Previous ADL + Current Period's Money Flow Volume

Sources:
    https://school.stockcharts.com/doku.php?id=technical_indicators:accumulation_distribution_line

</summary> */

public class ADL_Series : TSeries
{
    protected readonly TBars _data;
    private double _lastadl, _lastlastadl;

    //core constructors
    public ADL_Series()
    {
        Name = $"ADL()";
        _lastadl = _lastlastadl = 0;
    }
    public ADL_Series(TBars source)
    {
        _data = source;
        Name = $"ADL({(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _lastadl = _lastlastadl = 0;
        _data.Pub += Sub;
        Add(data: _data);
    }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false)
    {
        if (update) { this._lastadl = this._lastlastadl; }
        else { this._lastlastadl = this._lastadl; }

        double _adl = 0;
        double tmp = TBar.h - TBar.l;
        if (tmp > 0.0)
        {
            _adl = _lastadl + ((2 * TBar.c - TBar.l - TBar.h) / tmp * TBar.v);
        }
        _lastadl = _adl;

        var ret = (TBar.t, _adl);
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
        _lastadl = _lastlastadl = 0;
    }
}