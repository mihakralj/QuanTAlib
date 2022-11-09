namespace QuanTAlib;
using System;

/* <summary>
BBANDS: Bollinger Bands®
    Price channels created by John Bollinger, depict volatility as standard deviation boundary 
    line range from a moving average of price. The bands automatically widen when volatility 
    increases and contract when volatility decreases. Their dynamic nature allows them to be 
    used on different securities with the standard settings.

    Mid Band = simple moving average (SMA)
    Upper Band = SMA + (standard deviation of price x multiplier) 
    Lower Band = SMA - (standard deviation of price x multiplier)
    Bandwidth = Width of the channel: (Upper-Lower)/SMA
    %B = The location of the data point within the channel: (Price-Lower)/(Upper/Lower)
    Z-Score = number of standard deviations of the data point from SMA

Sources:
    https://www.investopedia.com/terms/b/bollingerbands.asp
    https://school.stockcharts.com/doku.php?id=technical_indicators:bollinger_bands

Note:
    Bollinger Bands® is a registered trademark of John A. Bollinger.

</summary> */

public class BBANDS_Series : Single_TSeries_Indicator
{
	public SMA_Series Mid { get; }
	public ADD_Series Upper { get; }
	public SUB_Series Lower { get; }
	public DIV_Series PercentB { get; }
	public DIV_Series Bandwidth { get; }
	public DIV_Series Zscore { get; }

	private readonly SDEV_Series _sdev;
	private readonly MUL_Series _mulsdev;
	private readonly SUB_Series _pbdnd;
	private readonly SUB_Series _pbdvr;
	private readonly SUB_Series _zdnd;

	public BBANDS_Series(TSeries source, int period = 26, double multiplier = 2.0, bool useNaN = false)
		: base(source, period: 0, useNaN)
	{
		this.Mid = new(source: source, period: period, useNaN: useNaN);

		_sdev = new(source, period, useNaN: useNaN);
		_mulsdev = new(_sdev, multiplier);
		this.Upper = new(Mid, _mulsdev);
		this.Lower = new(Mid, _mulsdev);

		_pbdnd = new(source, Lower);
		_pbdvr = new(Upper, Lower);

		this.PercentB = new(_pbdnd, _pbdvr);
		this.Bandwidth = new(_pbdvr, Mid);

		_zdnd = new(source, Mid);
		this.Zscore = new(_zdnd, _sdev);

		if (source.Count > 0)
		{ base.Add(this.Bandwidth); }
	}
	public override void Add((System.DateTime t, double v) TValue, bool update)
	{
		double _bbandwidth;
		if (update)
		{ _sdev.Add(TValue, true); }
		_bbandwidth = this.Bandwidth[(this.Count < this.Bandwidth.Count) ? this.Count : this.Bandwidth.Count - 1].v;
		var result = (TValue.t, _bbandwidth);
		base.Add(result, update);
	}
}