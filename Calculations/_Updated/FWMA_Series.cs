namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using System.Linq;

/* <summary>
FWMA: Fibonacci's Weighted Moving Average is similar to a Weighted Moving Average
		(WMA) where the weights are based on the Fibonacci Sequence.

</summary> */
public class FWMA_Series : TSeries {
	private readonly List<double> _buffer = new();
	private List<double> _weights;
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;
	protected int _len;

	public FWMA_Series(int period, bool useNaN) {
		_period = period;
		_NaN = useNaN;
		Name = $"FWMA({period})";
		_len = 0;
		_weights = CalculateWeights(_period);
	}

	public FWMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}

	public FWMA_Series() : this(period: 0, useNaN: false) { }
	public FWMA_Series(int period) : this(period: period, useNaN: false) { }
	public FWMA_Series(TBars source) : this(source.Close, 0, false) { }
	public FWMA_Series(TBars source, int period) : this(source.Close, period, false) { }
	public FWMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public FWMA_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);
		if (_period == 0) {
			_len++;
			_weights = CalculateWeights(_len);
		}
		double _fwma = 0;
		double totalWeights = _weights.Sum();
		object lockObj = new object();
		Parallel.For(0, _buffer.Count, i =>
		{
			double temp = _buffer[i] * _weights[i];
			lock (lockObj) { _fwma += temp; }
		});
		_fwma /= totalWeights;
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _fwma);
		return base.Add(res, update);
	}
	public override (DateTime t, double v) Add(TSeries data) {
		if (data == null) { return (DateTime.Today, Double.NaN); }
		foreach (var item in data) { Add(item, false); }
		return _data.Last;
	}
	public (DateTime t, double v) Add() {
		return Add(TValue: _data.Last, update: false);
	}
	private new void Sub(object source, TSeriesEventArgs e) {
		Add(TValue: _data.Last, update: e.update);
	}

	private static List<double> CalculateWeights(int period) {
		//to prevent overflow, max period can be no more than 1476
		period = (period > 1476) ? 1476 : period;
		List<double> weights = new List<double>(period);
		BigInteger a = 0;
		BigInteger b = 1;
		for (int i = 0; i < period; i++) {
			BigInteger temp = a;
			a = b;
			b = temp + b;
			weights.Add((double)Decimal.Parse(a.ToString()));
		}
		return weights;
	}

	public override void Reset() {
		_weights = CalculateWeights(_period);
		_buffer.Clear();
	}
}
