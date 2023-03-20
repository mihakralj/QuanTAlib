using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class JMA_chart : Indicator {
	#region Parameters

	[InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
	private int Period = 10;

	[InputParameter("Data source", 1, variants: new object[]
		{ "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
			"OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
	private int DataSource = 3
		;
	[InputParameter("Slope calc", 2, 2, 10, 1, 1)]
	private int SlopePeriod = 3;


	#endregion Parameters

	private TBars bars;

	///////
	private JMA_Series indicator;
	private LINREG_Series slope;
	///////

	public JMA_chart() {
		this.SeparateWindow = false;
		this.Name = "JMA - Jurik Moving Avg";
		this.Description = "Jurik Moving Average description";
		this.AddLineSeries("JMA", Color.Blue, 4, LineStyle.Solid);
	}


	protected override void OnInit() {
		this.bars = new();
		this.indicator = new(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
		this.slope = new(source: this.indicator, period: this.SlopePeriod);
	}

	protected override void OnUpdate(UpdateArgs args) {
		bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);

		this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
									this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low),
									this.GetPrice(PriceType.Close), this.GetPrice(PriceType.Volume), update);
		double result = this.indicator[this.indicator.Count - 1].v;

	  this.LinesSeries[0].SetMarker(offset: 0,color: this.slope > 0 ? Color.FromArgb(0,160,0) : Color.FromArgb(255, 0, 0));
		this.SetValue(result, lineIndex: 0);
	}
}
