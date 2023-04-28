namespace QuanTAlib;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/* <summary>
DWMA: Double Weighted Moving Average
    The weights are decreasing over the period with p^2 decay
		and the most recent data has the heaviest weight.

</summary> */

public class DWMA_Series : TSeries {
	private readonly List<double> _buffer = new();
	private List<double> _weights = new();
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;
	protected int _len;

//core constructors
	public DWMA_Series(int period, bool useNaN) : base() {
		_period = period;
		_NaN = useNaN;
		Name = $"DWMA({period})";
		_len = 0;
		_weights = CalculateWeights(_period);
	}

	public DWMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}

	public DWMA_Series() : this(0, false) {
	}

	public DWMA_Series(int period) : this(period, false) {
	}

	public DWMA_Series(TBars source) : this(source.Close, 0, false) {
	}

	public DWMA_Series(TBars source, int period) : this(source.Close, period, false) {
	}

	public DWMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) {
	}

	public DWMA_Series(TSeries source, int period) : this(source, period, false) {
	}

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		BufferTrim(_buffer, TValue.v, _period, update);
		if (_period == 0) {
			_len++;
			_weights = CalculateWeights(_len);
		}

		double _dwma = 0, _wsum = 0;
		var bufferCount = _buffer.Count;

		var lockObj = new object();
		Parallel.For(0, bufferCount, i =>
		{
			var temp = _buffer[i] * _weights[i];
			lock (lockObj) {
				_dwma += temp;
				_wsum += _weights[i];
			}
		});
		_dwma /= _wsum;
		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _dwma);
		return base.Add(res, update);
	}

	public override (DateTime t, double v) Add(TSeries data) {
		if (data == null) {
			return (DateTime.Today, double.NaN);
		}

		foreach (var item in data) {
			Add(item, false);
		}

		return _data.Last;
	}

	public new (DateTime t, double v) Add((DateTime t, double v) TValue) {
		return Add(TValue, false);
	}

	public (DateTime t, double v) Add(bool update) {
		return Add(_data.Last, update);
	}

	public (DateTime t, double v) Add() {
		return Add(_data.Last, false);
	}

	private new void Sub(object source, TSeriesEventArgs e) {
		Add(_data.Last, e.update);
	}

	//calculating weights
	private static List<double> CalculateWeights(int period) {
		var weights = new List<double>(period);
		for (var i = 0; i < period; i++) {
			weights.Add((i + 1) * (i + 1));
		}

		return weights;
	}

	//reset calculation
	public override void Reset() {
		_len = 0;
		_buffer.Clear();
		_weights = CalculateWeights(_period);
	}
}