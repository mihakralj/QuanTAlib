namespace QuanTAlib;
using System;

/* <summary>
DWMA: Double (linearly) Weighted Moving Average
    The weights are linearly decreasing over the period and the most recent data has
    the heaviest weight.

Sources:
    

</summary> */

public class DWMA_Series : Single_TSeries_Indicator
{
    public DWMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        for (int i = 0; i < this._p; i++) { this._weights.Add(i + 1); }
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer1 = new();
	private readonly System.Collections.Generic.List<double> _buffer2 = new();
	private readonly System.Collections.Generic.List<double> _weights = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer1, TValue.v, _p, update);
        double _wma = 0;
        for (int i = 0; i < _buffer1.Count; i++) { _wma += _buffer1[i] * this._weights[i]; }
        _wma /= (this._buffer1.Count * (this._buffer1.Count + 1)) * 0.5;

		Add_Replace_Trim(_buffer2, TValue.v, _p, update);
		double _dwma = 0;
		for (int i = 0; i < _buffer2.Count; i++) { _dwma += _buffer2[i] * this._weights[i]; }
		_dwma /= (this._buffer2.Count * (this._buffer2.Count + 1)) * 0.5;

		base.Add((TValue.t, _dwma), update, _NaN);
    }
}