namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
OBV: On-Balance Volume
    On-balance volume (OBV) is a technical trading momentum indicator that uses volume flow to predict
    changes in stock price. Joseph Granville first developed the OBV metric in the 1963 book 
    Granville's New Key to Stock Market Profits.

                          | +volume; if close > close[previous]
    OBV = OBV[previous] + | 0;       if close = close[previous]
                          | -volume; if close < close[previous]

Sources:
    https://www.investopedia.com/terms/o/onbalancevolume.asp
    https://www.tradingview.com/wiki/On_Balance_Volume_(OBV)  
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/on-balance-volume-obv/
    https://www.motivewave.com/studies/on_balance_volume.htm

Note:
    There is no consensus on what is the first OBV value in the series:
    - TA-LIB uses the first volume: OBV[0] = volume[0]
    - Skender stock library uses 0: OBV[0] = 0

</summary> */

public class OBV_Series : TSeries
{
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TBars _data;
    private double _lastobv, _lastlastobv;
    private double _lastclose, _lastlastclose;

    //core constructors
    public OBV_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"OBV({period})";
        this._lastobv = this._lastlastobv = 0;
        this._lastclose = this._lastlastclose = 0;
    }
    public OBV_Series(TBars source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(data: _data);
    }
    public OBV_Series() : this(period: 2, useNaN: false) { }
    public OBV_Series(int period) : this(period: period, useNaN: false) { }
    public OBV_Series(TBars source) : this(source, period: 2, useNaN: false) { }
    public OBV_Series(TBars source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false)
    {

        if (update)
        {
            this._lastobv = this._lastlastobv;
            this._lastclose = this._lastlastclose;
        }

        double _obv = this._lastobv;
        if (TBar.c > this._lastclose) { _obv += TBar.v; }
        if (TBar.c < this._lastclose) { _obv -= TBar.v; }

        this._lastlastobv = this._lastobv;
        this._lastobv = _obv;

        this._lastlastclose = this._lastclose;
        this._lastclose = TBar.c;

        var res = (TBar.t, (this.Count < this._period && this._NaN) ? double.NaN : _obv);
        return base.Add(res, update);
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
        this._lastobv = this._lastlastobv = 0;
        this._lastclose = this._lastlastclose = 0;
    }
}