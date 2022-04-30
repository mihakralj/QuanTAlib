namespace QuanTAlib;
using System;

/* <summary>
RSI: Relative Strength Index
    Created by J. Welles Wilder, the Relative Strength Index measures strength
    of the winning/losing streak over N lookback periods on a scale of 0 to 100,
    to depict overbought and oversold conditions.

Sources:
    https://www.investopedia.com/terms/r/rsi.asp

</summary> */

public class RSI_Series : Single_TSeries_Indicator
{
    private readonly System.Collections.Generic.List<double> _gain = new();
    private readonly System.Collections.Generic.List<double> _loss = new();
    private double _avgGain;
    private double _avgLoss;
    private double _lastValue;
    private double _lastlastValue;

    public RSI_Series(TSeries source, int period = 10, bool useNaN = false) : base(source, period: period, useNaN: useNaN)
    { if (source.Count > 0) { base.Add(source); } }

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        int i = this.Count;
        double _rsi = 0;
        if (update) { _lastValue = _lastlastValue; }
        if (i == 0) { _lastValue = TValue.v; }

        double _gainval = (TValue.v > _lastValue) ? TValue.v - _lastValue : 0;
        if (update) { _gain[_gain.Count - 1] = _gainval; } else { _gain.Add(_gainval); }
        if (_gain.Count > this._p) { _gain.RemoveAt(0); }

        double _lossval = (TValue.v < _lastValue) ? _lastValue - TValue.v : 0;
        if (update) { _loss[_loss.Count - 1] = _lossval; } else { _loss.Add(_lossval); }
        if (_loss.Count > this._p) { _loss.RemoveAt(0); }

        _lastlastValue = _lastValue;
        _lastValue = TValue.v;

        // calculate RSI
        if (i > _p)
        {
            _avgGain = ((_avgGain * (_p - 1)) + _gain[_gain.Count - 1]) / _p;
            _avgLoss = ((_avgLoss * (_p - 1)) + _loss[_loss.Count - 1]) / _p;
            if (_avgLoss > 0) {
                double rs = _avgGain / _avgLoss;
                _rsi = 100 - (100 / (1 + rs));
            }
            else { _rsi = 100; }
        }
        // initialize average gain
        else
        {
            double _sumGain = 0;
            for (int p = 0; p < _gain.Count; p++) { _sumGain += _gain[p]; }
            double _sumLoss = 0;
            for (int p = 0; p < _loss.Count; p++) { _sumLoss += _loss[p]; }

            _avgGain = _sumGain / _gain.Count;
            _avgLoss = _sumLoss / _loss.Count;

            _rsi = (_avgLoss > 0) ? 100 - (100 / (1 + (_avgGain / _avgLoss))) : 100;
        }

        var result = (TValue.t, (this.Count < this._p && this._NaN) ? double.NaN : _rsi);
        base.Add(result, update);
    }
}