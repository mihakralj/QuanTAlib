using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
CMO: Chande Momentum Oscillator
	Chande Momentum Oscillator (also known as CMO indicator) was developed by Tushar S. Chande
	CMO is similar to other momentum oscillators (e.g. RSI or Stochastics). Alike RSI oscillator,
	the CMO values move in the range from -100 to +100 points and its aim is to detect the
	overbought and oversold market conditions. CMO calculates the price momentum on both the up
	days as well as the down days. The CMO calculation is based on non-smoothed price values
	meaning that it can reach its extremes more frequently and the short-time swings are more visible.

Sources:
    https://www.technicalindicators.net/indicators-technical-analysis/144-cmo-chande-momentum-oscillator

</summary> */

public class CMO_Series : TSeries
{
    private readonly System.Collections.Generic.List<double> _buff_up = new();
    private readonly System.Collections.Generic.List<double> _buff_dn = new();
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private double _plast_value, _last_value;

    //core constructors
    public CMO_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"CMO({period})";
    }
    public CMO_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public CMO_Series() : this(period: 0, useNaN: false) { }
    public CMO_Series(int period) : this(period: period, useNaN: false) { }
    public CMO_Series(TBars source) : this(source.Close, 0, false) { }
    public CMO_Series(TBars source, int period) : this(source.Close, period, false) { }
    public CMO_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public CMO_Series(TSeries source) : this(source, 0, false) { }
    public CMO_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        if (update) { _last_value = _plast_value; } else { _plast_value = _last_value; }
        BufferTrim(buffer: _buff_up, (TValue.v > _last_value) ? TValue.v - _last_value : 0, period: _period, update: update);
        BufferTrim(buffer: _buff_dn, (TValue.v < _last_value) ? _last_value - TValue.v : 0, period: _period, update: update);
        _last_value = TValue.v;
        double _cmo_up = 0;
        double _cmo_dn = 0;
        for (int i = 0; i < Math.Min(_buff_up.Count, _buff_dn.Count); i++)
        {
            _cmo_up += _buff_up[i];
            _cmo_dn += _buff_dn[i];
        }
        double _cmo = 100 * (_cmo_up - _cmo_dn) / (_cmo_up + _cmo_dn);
        if (_cmo_up + _cmo_dn == 0) { _cmo = 0; }

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _cmo);
        return base.Add(res, update);
    }

    public override (DateTime t, double v) Add(TSeries data)
    {
        if (data == null) { return (DateTime.Today, Double.NaN); }
        foreach (var item in data) { Add(item, false); }
        return _data.Last;
    }

    public (DateTime t, double v) Add(bool update)
    {
        return this.Add(TValue: _data.Last, update: update);
    }
    public (DateTime t, double v) Add()
    {
        return Add(TValue: _data.Last, update: false);
    }
    private new void Sub(object source, TSeriesEventArgs e)
    {
        Add(TValue: _data.Last, update: e.update);
    }

    //reset calculation
    public override void Reset()
    {
        _buff_up.Clear();
        _buff_dn.Clear();
    }
}