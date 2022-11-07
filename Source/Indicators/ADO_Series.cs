namespace QuanTAlib;
using System;

/* <summary>
ADO: Chaikin Accumulation/Distribution Oscillator
    ADO measures the momentum of ADL using the difference between slow (10-day) EMA(ADL)
    and fast (3-day) EMA(ADL):

    Chaikin A/D Oscillator = (3-day EMA of ADL)  -  (10-day EMA of ADL)

Sources:
    https://school.stockcharts.com/doku.php?id=technical_indicators:chaikin_oscillator

</summary> */

public class ADO_Series : Single_TBars_Indicator
{
	private readonly ADL_Series _TSadl;

	private readonly EMA_Series _TSslow;
	private readonly EMA_Series _TSfast;
	private readonly SUB_Series _TSado;

	public ADO_Series(TBars source, bool useNaN = false) : base(source, period: 0, useNaN)
	{
		_TSadl = new(source: source, useNaN: false);
		_TSslow = new(source: _TSadl, period: 10, useNaN: false);
		_TSfast = new(source: _TSadl, period: 3, useNaN: false);
		_TSado = new(_TSfast, _TSslow);

		if (source.Count > 0)
		{ base.Add(_TSado); }
		Console.WriteLine(base.Count);
	}

	public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update)
	{
		if (update)
		{ _TSadl.Add(TBar, true); }

		double _ado = this._TSado[(this.Count < this._TSado.Count) ? this.Count : this._TSado.Count - 1].v;
		var result = (TBar.t, _ado);
		base.Add(result, update);
	}
}