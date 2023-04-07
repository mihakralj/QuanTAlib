namespace QuanTAlib;
using System;

/* <summary>
EQUITY - Generates P&L portfolio based on trades signals and equity prices
    
</summary> */


//base prices: bars.close
//trade signals: trades
//optional: long, short, long&short
//optional: warmup period: warmup

public class EQUITY_Series : Single_TSeries_Indicator {
	int trade_state = 0;
	readonly int _warmup = 0;
	double eq_value = 0;
	readonly TSeries _prices;
	readonly bool _long, _short;
	public EQUITY_Series(TSeries trades, TSeries prices, bool Long = true, bool Short = false, int Warmup = 0) : base(trades, period: 0, useNaN: false) {
		_prices = prices;
		_long = Long;
		_short = Short;
		_warmup = Warmup;
		if (base._data.Count > 0) { base.Add(base._data); }
	}

	public override void Add((System.DateTime t, double v) TValue, bool update) {
		if (this.Count != 0)
			eq_value = this[this.Count - 1].v;

		//buy signal
		if (TValue.v == 1 && this.Count > _warmup) {
			//we are not in-market and we can do long trades
			if (_short) { trade_state = 0; }
			if (_long) { trade_state = 1; }
		}

		//sell signal
		if (TValue.v == -1 && this.Count > _warmup) {
			//we are in-market and we can do long trades
			if (_long) { trade_state = 0; }
			if (_short) { trade_state = -1; }
		}

		if (trade_state == 1) {
			eq_value = this[this.Count - 1].v + (_prices[this.Count].v - _prices[this.Count - 1].v);

		}

		if (trade_state == -1) {
			eq_value = this[this.Count - 1].v + (_prices[this.Count - 1].v - _prices[this.Count].v);

		}
		base.Add((TValue.t, eq_value), update, _NaN);
	}
}