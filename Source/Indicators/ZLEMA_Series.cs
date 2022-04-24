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
	private readonly System.Collections.Generic.List<double> _buffer = new();
	private readonly double _k, _k1m;
	private double _lastema, _lastlastema;

	public ZLEMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
	{
		this._k = 2.0 / (this._p + 1);
		this._k1m = 1.0 - this._k;
		this._lastema = this._lastlastema = double.NaN;
		if (base._data.Count > 0)
		{ base.Add(base._data); }
	}

	public override void Add((System.DateTime t, double v) TValue, bool update)
	{
		int _lag = (int)((_p - 1) * 0.5);
		_lag = (this.Count - _lag < 0) ? 0 : this.Count - _lag;
		double _zl = TValue.v + (TValue.v - _data[_lag].v);
		double _ema = 0;
		if (update)
		{ this._lastema = this._lastlastema; }
		if (this.Count < this._p)
		{
			if (update)
			{ this._buffer[this._buffer.Count - 1] = _zl; }
			else
			{
				this._buffer.Add(_zl);
			}
			if (this._buffer.Count > this._p)
			{ this._buffer.RemoveAt(0); }

			for (int i = 0; i < this._buffer.Count; i++)
			{ _ema += this._buffer[i]; }
			_ema /= this._buffer.Count;
		}
		else
		{
			_ema = TValue.v * this._k + this._lastema * this._k1m;
		}

		this._lastlastema = this._lastema;
		this._lastema = _ema;

		var ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _ema);
		base.Add(ret, update);
	}
}