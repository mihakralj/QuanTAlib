namespace QuanTAlib;
using System;

/* <summary>
CCI: Commodity Channel Index 
    Commodity Channel Index is a momentum oscillator used to primarily identify overbought
    and oversold levels relative to a mean. CCI measures the current price level relative
    to an average price level over a given period of time:
    - CCI is relatively high when prices are far above their average.
    - CCI is relatively low when prices are far below their average.
    Using this method, CCI can be used to identify overbought and oversold levels.

Sources:
    https://www.investopedia.com/terms/c/commoditychannelindex.asp
    https://www.fidelity.com/learning-center/trading-investing/technical-analysis/technical-indicator-guide/cci

</summary> */

public class CCI_Series : Single_TBars_Indicator
{
  private readonly System.Collections.Generic.List<double> _tp = new();

  public CCI_Series(TBars source, int period = 10, bool useNaN = false) 
    : base(source, period: period, useNaN: useNaN) {

    if (_bars.Count > 0) { base.Add(_bars); }
  }

  public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update) {
    
    double _tpItem = (TBar.h + TBar.l + TBar.c) / 3.0;
    if (update) { this._tp[this._tp.Count - 1] = _tpItem; } else { this._tp.Add(_tpItem); }
    if (this._tp.Count > this._p) { this._tp.RemoveAt(0); }
    
    // average TP over _tp buffer
    double _avgTp = 0;
    for (int i = 0; i < this._tp.Count; i++) { _avgTp+=this._tp[i]; }
    _avgTp /= this._tp.Count;

    // average Deviation over _tp buffer
    double _avgDv = 0;
    for (int i = 0; i < this._tp.Count; i++) { _avgDv += Math.Abs(_avgTp - this._tp[i]); }
    _avgDv /= this._tp.Count;

    double _cci =  (_avgDv == 0) ? double.NaN : (this._tp[this._tp.Count-1] - _avgTp) / (0.015 * _avgDv);

    var result = (TBar.t, (this.Count < this._p  && this._NaN) ? double.NaN : _cci);
    base.Add(result, update);
  }
}