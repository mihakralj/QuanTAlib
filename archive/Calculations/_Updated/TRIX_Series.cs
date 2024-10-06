namespace QuanTAlib;

using System;
using System.Linq;

/* <summary>
TRIX: Triple Exponential Average Oscillator
	Developed by Jack Hutson in the early 1980s, the triple exponential average (TRIX)
	has become a popular technical analysis tool to aid chartists in spotting diversions
	and directional cues in stock trading patterns. 

Sources:
	https://www.investopedia.com/terms/t/trix.asp

</summary> */

public class TRIX_Series : TSeries {
    private readonly double _k;
    private readonly System.Collections.Generic.List<double> _buffer1 = new();
    private readonly System.Collections.Generic.List<double> _buffer2 = new();
    private readonly System.Collections.Generic.List<double> _buffer3 = new();
    private double _lastema1, _lastema2, _lastema3;
    private double _llastema1, _llastema2, _llastema3;
    private int _len;
    private readonly bool _useSMA;
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;

    //core constructors

    public TRIX_Series(int period, bool useNaN, bool useSMA) {
        _period = period;
        _NaN = useNaN;
        _useSMA = useSMA;
        Name = $"TRIX({period})";
        _k = 2.0 / (_period + 1);
        _len = 0;
        _lastema1 = _llastema1 = _lastema2 = _llastema2 = _lastema3 = _llastema3 = 0;
    }
    public TRIX_Series(TSeries source, int period, bool useNaN, bool useSMA) : this(period, useNaN, useSMA) {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public TRIX_Series() : this(0, false, true) { }
    public TRIX_Series(int period) : this(period, false, true) { }
    public TRIX_Series(TBars source) : this(source.Close, 0, false) { }
    public TRIX_Series(TBars source, int period) : this(source.Close, period, false) { }
    public TRIX_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public TRIX_Series(TSeries source, int period) : this(source, period, false, true) { }
    public TRIX_Series(TSeries source, int period, bool useNaN) : this(source, period, useNaN, true) { }


    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
        if (double.IsNaN(TValue.v)) {
            return base.Add((TValue.t, Double.NaN), update);
        }
        if (_len == 0) { _lastema1 = _lastema2 = _lastema3 = TValue.v; }
        if (update) { _lastema1 = _llastema1; _lastema2 = _llastema2; _lastema3 = _llastema3; } else {
            _llastema1 = _lastema1; _llastema2 = _lastema2; _llastema3 = _lastema3; _len++;
        }

        double _ema1, _ema2, _ema3;
        if ((this.Count < _period) && _useSMA) {
            BufferTrim(_buffer1, TValue.v, _period, update);
            _ema1 = 0;
            for (int i = 0; i < _buffer1.Count; i++) { _ema1 += _buffer1[i]; }
            _ema1 /= _buffer1.Count;

            BufferTrim(_buffer2, _ema1, _period, update);
            _ema2 = 0;
            for (int i = 0; i < _buffer2.Count; i++) { _ema2 += _buffer2[i]; }
            _ema2 /= _buffer2.Count;

            BufferTrim(_buffer3, _ema2, _period, update);
            _ema3 = 0;
            for (int i = 0; i < _buffer3.Count; i++) { _ema3 += _buffer3[i]; }
            _ema3 /= _buffer3.Count;
        } else {
            _ema1 = (TValue.v - _lastema1) * _k + _lastema1;
            _ema2 = (_ema1 - _lastema2) * _k + _lastema2;
            _ema3 = (_ema2 - _lastema3) * _k + _lastema3;
        }
        double _trix = 100 * (_ema3 - _lastema3) / _lastema3;
        _lastema1 = _ema1;
        _lastema2 = _ema2;
        _lastema3 = _ema3;

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _trix);
        return base.Add(res, update);
    }

    //variation of Add()
    public override (DateTime t, double v) Add(TSeries data) {
        if (data == null) { return (DateTime.Today, Double.NaN); }
        foreach (var item in data) { Add(item, false); }
        return _data.Last;
    }
    public (DateTime t, double v) Add(bool update) {
        return this.Add(TValue: _data.Last, update: update);
    }
    public (DateTime t, double v) Add() {
        return Add(TValue: _data.Last, update: false);
    }
    private new void Sub(object source, TSeriesEventArgs e) {
        Add(TValue: _data.Last, update: e.update);
    }

    //reset calculation
    public override void Reset() {
        _len = 0;
    }
}