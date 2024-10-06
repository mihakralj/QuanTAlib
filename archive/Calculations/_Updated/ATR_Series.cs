namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
ATR: wildeR Moving Average
    The average true range (ATR) is a price volatility indicator
    showing the average price variation of assets within a given time period.

Sources:
    https://en.wikipedia.org/wiki/Average_true_range
    https://www.tradingview.com/wiki/Average_True_Range_(ATR)
    https://www.investopedia.com/terms/a/atr.asp

</summary> */

public class ATR_Series : TSeries {
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TBars _data;
    private double _k;
    private int _len;
    private double _lastatr, _lastlastatr, _cm1, _lastcm1, _sum, _oldsum;

    //core constructors
    public ATR_Series(int period, bool useNaN) {
        _period = period;
        _k = 1.0 / (double)(_period);
        _NaN = useNaN;
        _len = 0;
        Name = $"ATR({period})";
    }
    public ATR_Series(TBars source, int period, bool useNaN) : this(period, useNaN) {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(data: _data);
    }
    public ATR_Series() : this(period: 1, useNaN: false) { }
    public ATR_Series(int period) : this(period: period, useNaN: false) { }
    public ATR_Series(TBars source) : this(source, period: 1, useNaN: false) { }
    public ATR_Series(TBars source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false) {
        if (update) { _lastatr = _lastlastatr; _cm1 = _lastcm1; _sum = _oldsum; } else {
            _lastlastatr = _lastatr; _lastcm1 = _cm1; _oldsum = _sum;
            _k = (_period == 0) ? 1 / (double)_len : _k;
            _len++;
        }

        if (_len == 1) { _cm1 = TBar.c; }
        double d1 = Math.Abs(TBar.h - TBar.l);
        double d2 = Math.Abs(_cm1 - TBar.h);
        double d3 = Math.Abs(_cm1 - TBar.l);
        (DateTime t, double v) d = (TBar.t, Math.Max(d1, Math.Max(d2, d3)));
        _cm1 = TBar.c;

        double _atr = 0;
        if (this.Count == 0) { _atr = d.v; } else if (this.Count < _period + 1) { _sum += d.v; _atr = _sum / (this.Count); } else { _atr = _k * (d.v - _lastatr) + _lastatr; }
        _lastatr = _atr;

        var res = (TBar.t, Count < _period - 1 && _NaN ? double.NaN : _atr);
        return base.Add(res, update);
    }

    public new void Add(TBars data) {
        foreach (var item in data) { Add(item, false); }
    }
    public (DateTime t, double v) Add(bool update) {
        return this.Add(TBar: _data.Last, update: update);
    }
    public (DateTime t, double v) Add() {
        return Add(TBar: _data.Last, update: false);
    }
    private new void Sub(object source, TSeriesEventArgs e) {
        Add(TBar: _data.Last, update: e.update);
    }

    //reset calculation
    public override void Reset() {
        _len = 0;
    }
}