namespace QuanTAlib;
using System;
using System.Linq;
using System.Numerics;

/* <summary>
TRIX: Triple Exponential Average
	Developed by Jack Hutson in the early 1980s, the triple exponential average (TRIX)
	has become a popular technical analysis tool to aid chartists in spotting diversions
and directional cues in stock trading patterns. 


Calculation:
	Ema1 = Ema (Close);
    Ema2 = Ema (Ema1);
    Ema3 = Ema (Ema2);
    TRIX = (Ema3-Ema3[1]) / Ema3[1]

Sources:
	https://www.investopedia.com/terms/t/trix.asp

</summary> */
public class TRIX_Series : Single_TSeries_Indicator
{
  private readonly double _k, _k1m;
  private readonly System.Collections.Generic.List<double> _buffer1 = new();
  private readonly System.Collections.Generic.List<double> _buffer2 = new();
  private readonly System.Collections.Generic.List<double> _buffer3 = new();

  private double _lastema1, _lastema2, _lastema3;
  private double _llastema1, _llastema2, _llastema3;
  private bool _useSMA;

  public TRIX_Series(TSeries source, int period, bool useNaN = false, bool useSMA = true) : base(source, period, useNaN)
  {

    _k = 2.0 / (_p + 1);
    _k1m = 1.0 - _k;
    _lastema1 = _llastema1 = _lastema2 = _llastema2 = _lastema3 = _llastema3 = 0;
    _useSMA = useSMA;
    if (this._data.Count > 0) { base.Add(this._data); }
  }

  public override void Add((DateTime t, double v) TValue, bool update)
  {
    double _ema1, _ema2, _ema3;
    if (this.Count == 0) { _lastema1 = _lastema2 = _lastema3 = TValue.v; }

    if (update) { _lastema1 = _llastema1; _lastema2 = _llastema2; _lastema3 = _llastema3; }
    else { _llastema1 = _lastema1; _llastema2 = _lastema2; _llastema3 = _lastema3; }

    if ((this.Count < _p) && _useSMA)
    {
      Add_Replace(_buffer1, TValue.v, update);
      _ema1 = 0;
      for (int i = 0; i < _buffer1.Count; i++) { _ema1 += _buffer1[i]; }
      _ema1 /= _buffer1.Count;

      Add_Replace(_buffer2, _ema1, update);
      _ema2 = 0;
      for (int i = 0; i < _buffer2.Count; i++) { _ema2 += _buffer2[i]; }
      _ema2 /= _buffer2.Count;

      Add_Replace(_buffer3, _ema2, update);
      _ema3 = 0;
      for (int i = 0; i < _buffer3.Count; i++) { _ema3 += _buffer3[i]; }
      _ema3 /= _buffer3.Count;
    }
    else
    {
      _ema1 = (TValue.v * this._k) + (this._lastema1 * this._k1m);
      _ema2 = (_ema1 * this._k) + (this._lastema2 * this._k1m);
      _ema3 = (_ema2 * this._k) + (this._lastema3 * this._k1m);
    }
    double _trix = 100 * (_ema3 - _lastema3) / _lastema3;
    _lastema1 = _ema1;
    _lastema2 = _ema2;
    _lastema3 = _ema3;

    base.Add((TValue.t, _trix), update, _NaN);
  }
}