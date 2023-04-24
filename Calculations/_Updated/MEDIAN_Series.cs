using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
MED - Median value
    Median of numbers is the middlemost value of the given set of numbers. 
    It separates the higher half and the lower half of a given data sample. 
    At least half of the observations are smaller than or equal to median 
    and at least half of the observations are greater than or equal to the median.

    If the number of values is odd, the middlemost observation of the sorted
    list is the median of the given data.  If the number of values is even, 
    median is the average of (n/2)th and [(n/2) + 1]th values of the sorted list.

    If period = 0 => period is max

Sources:
    https://corporatefinanceinstitute.com/resources/knowledge/other/median/
    https://en.wikipedia.org/wiki/Median

</summary> */

public class MEDIAN_Series : TSeries {
	private readonly System.Collections.Generic.List<double> _buffer = new();
	protected readonly int _period;
	protected readonly bool _NaN;
	protected readonly TSeries _data;

	//core constructors
	public MEDIAN_Series(int period, bool useNaN) : base() {
		_period = period;
		_NaN = useNaN;
		Name = $"MEDIAN({period})";
	}
	public MEDIAN_Series(TSeries source, int period, bool useNaN) : this(period, useNaN) {
		_data = source;
		Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
		_data.Pub += Sub;
		Add(_data);
	}
	public MEDIAN_Series() : this(period: 0, useNaN: false) { }
	public MEDIAN_Series(int period) : this(period: period, useNaN: false) { }
	public MEDIAN_Series(TBars source) : this(source.Close, 0, false) { }
	public MEDIAN_Series(TBars source, int period) : this(source.Close, period, false) { }
	public MEDIAN_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
	public MEDIAN_Series(TSeries source) : this(source, 0, false) { }
	public MEDIAN_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

	//////////////////
	// core Add() algo
	public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false) {
		BufferTrim(buffer:_buffer, value:TValue.v, period:_period, update: update);

		System.Collections.Generic.List<double> _s = new(this._buffer);
		_s.Sort();
		int _p1 = _s.Count / 2;
		int _p2 = Math.Max(0, (_s.Count / 2) - 1);
		double _med = (_s.Count % 2 != 0) ? _s[_p1] : (_s[_p1] + _s[_p2]) / 2;

		var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _med);
		return base.Add(res, update);
	}

	public override (DateTime t, double v) Add(TSeries data) {
		if (data == null) { return (DateTime.Today, Double.NaN); }
		foreach (var item in data) { Add(item, false); }
		return _data.Last;
	}
	public new (DateTime t, double v) Add((DateTime t, double v) TValue) {
		return Add(TValue, false);
	}
	public (DateTime t, double v) Add(bool update) {
		return this.Add(TValue: _data.Last, update: update);
	}
	public (DateTime t, double v) Add() {
		return Add(TValue: _data.Last, update: false);
	}
	private new void Sub(object source, TSeriesEventArgs e) {
		Add(TValue: _data.Last, update: e.update);
	}

	//reset calculation
	public override void Reset() {
		_buffer.Clear();
	}
}