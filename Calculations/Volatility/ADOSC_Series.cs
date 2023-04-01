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


public class ADOSC_Series : Single_TBars_Indicator
{
    private readonly double _k1, _k2;
    private double _lastema1, _lastlastema1, _lastema2, _lastlastema2;
    private double _lastadl, _lastlastadl;

	public ADOSC_Series(TBars source, int shortPeriod = 3, int longPeriod =10, bool useNaN = false) : base(source, period: 0, useNaN)
	{
        _k1 = 2.0 / (shortPeriod + 1);
        _k2 = 2.0 / (longPeriod + 1);
        _lastadl = _lastlastadl = _lastema1 = _lastlastema1 = _lastema2 = _lastlastema2 = 0;
        if (_bars.Count > 0) { base.Add(_bars); }
    }

	public override void Add((DateTime t, double o, double h, double l, double c, double v) TBar, bool update)
	{
        if (update) { 
			_lastadl = _lastlastadl;
			_lastema1 = _lastlastema1;
            _lastema2 = _lastlastema2;
        }

        double _adl = 0;
        double tmp = TBar.h - TBar.l;
        if (tmp > 0.0) { _adl = _lastadl + ((2 * TBar.c - TBar.l - TBar.h) / tmp * TBar.v); }
        if (this.Count == 0) { _lastema1 = _lastema2 = _adl;  }

		double  _ema1 = (_adl - _lastema1) * _k1 + _lastema1;
        double _ema2 = (_adl - _lastema2) * _k2 + _lastema2;

        _lastlastadl = _lastadl; _lastadl = _adl;
        _lastlastema1 = _lastema1; _lastema1 = _ema1;
        _lastlastema2 = _lastema2; _lastema2 = _ema2;

        double _adosc = _ema1 - _ema2;
        base.Add((TBar.t, _adosc), update, _NaN);
    }
	
}
/*
public class ADOSC_Series : Single_TBars_Indicator
{
	private readonly ADL_Series _TSadl;

	private readonly EMA_Series _TSslow;
	private readonly EMA_Series _TSfast;
	private readonly SUB_Series _TSado;

	public ADOSC_Series(TBars source, bool useNaN = false) : base(source, period: 0, useNaN)
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
*/