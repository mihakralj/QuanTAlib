namespace QuanTAlib;
using System;

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

public class OBV_Series : Single_TBars_Indicator
{
	private double _lastobv, _lastlastobv;
	private double _lastclose, _lastlastclose;
	public OBV_Series(TBars source, int period = 10, bool useNaN = false) : base(source, period: period, useNaN: useNaN)
	{
		this._lastobv = this._lastlastobv = 0;
		this._lastclose = this._lastlastclose = 0;
		if (_bars.Count > 0) { base.Add(_bars); }
  }

  public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update) 
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

		var result = (TBar.t, (this.Count < this._p  && this._NaN) ? double.NaN : _obv);
    base.Add(result, update);
  }
}
