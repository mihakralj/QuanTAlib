using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
RSI: Relative Strength Index
    Created by J. Welles Wilder, the Relative Strength Index measures strength
    of the winning/losing streak over N lookback periods on a scale of 0 to 100,
    to depict overbought and oversold conditions.

Sources:
    https://www.investopedia.com/terms/r/rsi.asp

</summary> */

public class RSI_Series : TSeries {
    private readonly System.Collections.Generic.List<double> _gain = new();
    private readonly System.Collections.Generic.List<double> _loss = new();
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private double _avgGain, _avgLoss, _lastValue;
    private double _avgGain_o, _avgLoss_o, _lastValue_o;
    private int i;

    //core constructors
    public RSI_Series(int period, bool useNaN) {
        _period = period;
        _NaN = useNaN;
        Name = $"RSI({period})";
        i = 0;
    }
    public RSI_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public RSI_Series() : this(period: 0, useNaN: false) { }
    public RSI_Series(int period) : this(period: period, useNaN: false) { }
    public RSI_Series(TBars source) : this(source.Close, 0, false) { }
    public RSI_Series(TBars source, int period) : this(source.Close, period, false) { }
    public RSI_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public RSI_Series(TSeries source) : this(source, 0, false) { }
    public RSI_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {

        double _rsi = 0;
        if (update) {
            _lastValue = _lastValue_o;
            _avgGain = _avgGain_o;
            _avgLoss = _avgLoss_o;
        } else {
            _lastValue_o = _lastValue;
            _avgGain_o = _avgGain;
            _avgLoss_o = _avgLoss;
        }

        if (i == 0) { _lastValue = TValue.v; }

        double _gainval = (TValue.v > _lastValue) ? TValue.v - _lastValue : 0;
        BufferTrim(_gain, _gainval, _period, update);
        double _lossval = (TValue.v < _lastValue) ? _lastValue - TValue.v : 0;
        BufferTrim(_loss, _lossval, _period, update);
        _lastValue = TValue.v;

        // calculate RSI
        if (i > _period && _period != 0) {
            _avgGain = ((_avgGain * (_period - 1)) + _gain[^1]) / _period;
            _avgLoss = ((_avgLoss * (_period - 1)) + _loss[^1]) / _period;
            if (_avgLoss > 0) {
                double rs = _avgGain / _avgLoss;
                _rsi = 100 - (100 / (1 + rs));
            } else { _rsi = 100; }
        }
        // initialize average gain
        else {
            double _sumGain = 0;
            for (int p = 0; p < _gain.Count; p++) { _sumGain += _gain[p]; }
            double _sumLoss = 0;
            for (int p = 0; p < _loss.Count; p++) { _sumLoss += _loss[p]; }

            _avgGain = _sumGain / _gain.Count;
            _avgLoss = _sumLoss / _loss.Count;

            _rsi = (_avgLoss > 0) ? 100 - (100 / (1 + (_avgGain / _avgLoss))) : 100;
        }
        if (!update) { i++; }

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _rsi);
        return base.Add(res, update);
    }

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
        i = 0;
    }
}