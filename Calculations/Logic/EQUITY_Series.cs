namespace QuanTAlib;
using System;

/* <summary>
EQUITY - Generates P&L portfolio based on trades signals and equity prices
    
</summary> */


//base prices: bars.close
//trade signals: trades
//optional: long, short, long&short
//optional: warmup period: warmup

/*

public class EQUITY_Series : Single_TSeries_Indicator {
	readonly TSeries inmarket; //for every bar
	private readonly TSeries _price;
	private double _equity;
	private readonly double _capital;

	readonly int _warmup;
	double _cash;
	int _units;
	private bool _longbuy, _longsell;
	double _long_order, _open_order;
	double _investment_value;
	short _inmarket;

	public EQUITY_Series(TSeries signal, TSeries price, int warmup = 0, double capital = 1000) : base(signal, period: 0, useNaN: false) {
		_capital = capital;
		_cash = _capital;
		_investment_value = 0;
		_warmup = (warmup > 0) ? warmup : 1;

		inmarket = new();
		_longbuy = _longsell = false;
		_open_order = 0;
		_inmarket = 0;
		_units = 0;
		_long_order = 0;

		_price = price; //we buy on the Open price of the NEXT bar
		_long_order = 0;

		if (base._data.Count > 0) { base.Add(base._data); }
	}

	public override void Add((System.DateTime t, double v) TValue, bool update) {

		if (this.Count > _warmup) {

			// harvest the gain-loss from previous day
			_investment_value = _units * _price[this.Count - 1].v;
			_equity = _cash + _investment_value;


			//execute orders from previous bar
			if (_longbuy && _inmarket == 0) { //time to execute the long buy
				_units = (int)(_cash / _price[this.Count - 1].v);
				_long_order = _units * _price[this.Count - 1].v;
				_cash -= _long_order;
				_open_order = _long_order;
				_equity = _cash + _open_order;
				_inmarket = 1;
				_longbuy = false;
			}

			if (_longsell && _inmarket == 1) { //time to execute the long sell
				_long_order = (_units * _price[this.Count - 1].v);
				_cash += _long_order;
				_units = 0;

				_open_order = 0;
				_equity = _cash + _open_order;
				_inmarket = 0;
				_longsell = false;
			}

			if (_inmarket == 0 && TValue.v == 1) { _longbuy = true; }  //out of market, enter long
			if (_inmarket == 1 && TValue.v == -1) { _longsell = true; }  //long market, exit long

			//Console.WriteLine($"{TValue.v,3}\t {(_inmarket)} : {_cash,10:f2} + {_units*_price[this.Count-1].v,7:f2} = {_equity-_capital:f2}");
		}
		inmarket.Add((TValue.t, (double)_inmarket));
		base.Add((TValue.t, _equity), update, _NaN);
	}
}

*/