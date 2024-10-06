namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
MIDPRICE: Midpoint price (highhest high + lowest low)/2 in the given period in the series.
    If period = 0 => period = full length of the series

</summary> */

public class MIDPRICE_Series : TSeries {
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TBars _data;
    private readonly System.Collections.Generic.List<double> _bufferhi = new();
    private readonly System.Collections.Generic.List<double> _bufferlo = new();

    //core constructors
    public MIDPRICE_Series(int period, bool useNaN) {
        _period = period;
        _NaN = useNaN;
        Name = $"MIDPRICE({period})";
    }
    public MIDPRICE_Series(TBars source, int period, bool useNaN) : this(period, useNaN) {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(data: _data);
    }
    public MIDPRICE_Series() : this(period: 2, useNaN: false) { }
    public MIDPRICE_Series(int period) : this(period: period, useNaN: false) { }
    public MIDPRICE_Series(TBars source) : this(source, period: 2, useNaN: false) { }
    public MIDPRICE_Series(TBars source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update = false) {
        BufferTrim(_bufferhi, TBar.h, _period, update);
        BufferTrim(_bufferlo, TBar.l, _period, update);
        double _mid = (_bufferhi.Max() + _bufferlo.Min()) * 0.5;

        var res = (TBar.t, Count < _period - 1 && _NaN ? double.NaN : _mid);
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
        _bufferhi.Clear();
        _bufferlo.Clear();
    }
}