namespace QuanTAlib;
using System;

/* <summary>
ZLEMA: Zero Lag Exponential Moving Average
    The Zero lag exponential moving average (ZLEMA) indicator was created by John
    Ehlers and Ric Way.

The formula for a given N-Day period and for a given Data series is:
    Lag = (Period-1)/2
    Ema Data = {Data+(Data-Data(Lag days ago))
    ZLEMA = EMA (EmaData,Period)

Remark:
    The idea is do a regular exponential moving average (EMA) calculation but on a
    de-lagged data instead of doing it on the regular data. Data is de-lagged by
    removing the data from "lag" days ago thus removing (or attempting to remove)
    the cumulative lag effect of the moving average.

</summary> */

public class ZLEMA_Series : Single_TSeries_Indicator
{
	private ZL_Series zlag;
	private EMA_Series ema;

  public ZLEMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
  {
	  zlag = new(source: source, period: period, useNaN: false);
	  ema = new EMA_Series(source: source, period: period, useNaN: useNaN);
  }

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
    (System.DateTime t, double v) result = ema[ema.Count-1];
        base.Add(result, update);

    }
}
