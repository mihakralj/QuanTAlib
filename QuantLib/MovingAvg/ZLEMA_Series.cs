using System;
namespace QuantLib;

/**
ZLEMA: Zero Lag Exponential Moving Average

The Zero lag exponential moving average (ZLEMA) indicator was created by John
Ehlers and Ric Way.

The formula for a given N-Day period and for a given Data series is:
Lag = (Period-1)/2
Ema Data = {Data+(Data-Data(Lag days ago))
ZLEMA = EMA (EmaData,Period)

The idea is do a regular exponential moving average (EMA) calculation but on a
de-lagged data instead of doing it on the regular data. Data is de-lagged by
removing the data from "lag" days ago thus removing (or attempting to remove)
the cumulative lag effect of the moving average.
**/

public class ZLEMA_Series : Single_TSeries_Indicator
{
	private readonly double _k;
	private double _dm1, _dm2, _olddm2;
	private readonly double _k1m;
	private double _lastema, _lastlastema;

	public ZLEMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
	{
		this._k = 2.0 / (double)(period + 1);
		this._k1m = 1.0 - this._k;
		this._lastema = this._lastlastema = this._olddm2 = double.NaN;
		this._dm1 = this._dm2 = 0;

		if (base._data.Count > 0) { base.Add(base._data); }
	}

	public override void Add((System.DateTime t, double v) d, bool update)
	{
		if (update)
		{
			this._lastema = this._lastlastema;
			this._dm1 = this._dm2;
			this._dm2 = this._olddm2;
		}

		double _lagdata = d.v + (d.v - _dm2);

		double _ema = System.Double.IsNaN(this._lastema) ? _lagdata : _lagdata * this._k + this._lastema * this._k1m;
		this._lastlastema = this._lastema;
		this._lastema = _ema;
		this._olddm2 = this._dm2;
		this._dm2 = this._dm1;
		this._dm1 = d.v;

		(System.DateTime t, double v) result =
			(d.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ema);
		base.Add(result, update);

	}
}
