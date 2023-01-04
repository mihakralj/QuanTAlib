namespace QuanTAlib;
using System;

/* <summary>
FMA: Fibonacci Moving Average
    FMA calculates the average across multiple EMAs with periods following Fibonacci sequence
		(skipping initial Fibonacci numbers of 1, 1, 2) 3, 5, 8, 13, 21, 34...

		FMA(n) = Average(EMA(3), EMA(5), EMA(8), ema(13), ... EMA(n-th Fib))

Sources:
    https://kaabar-sofien.medium.com/the-fibonacci-moving-average-the-full-guide-60e718117595
	  https://www.tradingview.com/script/6pxgp2vh-Fibonacci-Moving-Average-FMA/

</summary> */

public class FMA_Series : Single_TSeries_Indicator {
	readonly double[,] fib;
	double _oldsum;
	readonly int _len;

	public FMA_Series(TSeries source, int period) : base(source, period, false) {
		_len = period;
		fib = new double[_len, 4];
		int a = 3;
		int b = 5;
		int f = 0;
		fib[0, 0] = 2 / ((double)a - 1);
		if (_len > 1) { fib[1, 0] = 2 / ((double)b - 1); }
		if (_len > 2) {
			for (int i = 2; i < _len; i++) {
				f = a + b;
				a = b;
				b = f;
				fib[i, 0] = 2 / ((double)f - 1);
			}
		}
		_oldsum = 0;
		if (this._data.Count > 0) { base.Add(this._data); }
	}

	public override void Add((DateTime t, double v) TValue, bool update) {
		double _sum = 0;
		for (int i = 0; i < _len; i++) {
			if (update) { fib[i, 1] = fib[i, 3]; _sum = _oldsum; }
			else { fib[i, 3] = fib[i, 1]; _oldsum = _sum; }

			if (this.Count == 0) { fib[i, 1] = TValue.v; }
			else {
				fib[i, 2] = fib[i, 0] * (TValue.v - fib[i, 1]) + fib[i, 1];
				fib[i, 1] = fib[i, 2];
			}
			_sum += fib[i, 1];
		}

		double _fma = _sum / _len;
		base.Add((TValue.t, _fma), update, _NaN);
	}
}