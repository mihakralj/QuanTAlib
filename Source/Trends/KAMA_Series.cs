namespace QuanTAlib;
using System;

/* <summary>
KAMA: Kaufman's Adaptive Moving Average
    Created in 1988 by American quantitative finance theorist Perry J. Kaufman and is known as
    Kaufman's Adaptive Moving Average (KAMA). Even though the method was developed as early as 1972,
    it was not until the popular book titled "Trading Systems and Methods" that it was made widely
    available to the public. Unlike other conventional moving averages systems, the Kaufman's Adaptive
    Moving Average, considers market volatility apart from price fluctuations.

    KAMAi = KAMAi - 1 + SC * ( price - KAMAi-1 )

Sources:
    https://www.tutorialspoint.com/kaufman-s-adaptive-moving-average-kama-formula-and-how-does-it-work
    https://corporatefinanceinstitute.com/resources/knowledge/trading-investing/kaufmans-adaptive-moving-average-kama/
    https://www.technicalindicators.net/indicators-technical-analysis/152-kama-kaufman-adaptive-moving-average

Remark:
    If useNaN:true argument is provided, KAMA starts calculating values from [period] bar onwards.
    Without useNaN argument (default setting), KAMA starts calculating values from bar 1 - and yields
    slightly different results for the first 50 bars - and then converges with the other one.

</summary> */

public class KAMA_Series : Single_TSeries_Indicator
{
  private readonly double _scFast, _scSlow;
  private readonly System.Collections.Generic.List<double> _buffer = new();
  private double _lastkama = double.NaN;
  private double _lastlastkama;

  public KAMA_Series(TSeries source, int period, int fast = 2, int slow= 30, bool useNaN = false) : base(source, period, useNaN) {
    _scFast = 2.0 / (fast+1);
    _scSlow = 2.0 / (slow+1);
    if (base._data.Count > 0) { base.Add(base._data); }
  }
  public override void Add((System.DateTime t, double v) TValue, bool update)
  {
    if (update){
      _buffer[_buffer.Count - 1] = TValue.v;
      this._lastkama = this._lastlastkama;
    } else {
      _buffer.Add(TValue.v);
    }
    if (_buffer.Count > _p + 1) { _buffer.RemoveAt(0); }
    double _kama = 0;
    if (this.Count < this._p) {
        for (int i = 0; i < this._buffer.Count; i++) { _kama += this._buffer[i]; }
        _kama /= this._buffer.Count;
    } else {
      double _change = Math.Abs(_buffer[_buffer.Count - 1] - _buffer[(_buffer.Count > _p + 1) ? 1 : 0]);
      double _sumpv = 0;
      for (int i = 1; i < _buffer.Count; i++)
      { _sumpv += Math.Abs(_buffer[(_buffer.Count > 0) ? i : 0] - _buffer[i - 1]); }
      double _er = (_sumpv == 0) ? 0 : _change / _sumpv;
      double _sc = (_er * (_scFast - _scSlow)) + _scSlow;
      _kama = (_lastkama + (_sc * _sc * (TValue.v - _lastkama)));
    }
    _lastlastkama = _lastkama;
    _lastkama = _kama;
    var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _kama);
    base.Add(result, update);
  }
}