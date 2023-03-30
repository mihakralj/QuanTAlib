using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class JMA_chart : Indicator {
	#region Parameters

	[InputParameter("Data source", 0, variants: new object[]
		{ "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
			"OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
	private int DataSource = 3;

	[InputParameter("Smoothing period", 1, 1, 999, 1, 1)]
	private int Period = 10;

	[InputParameter("Volatility short", 2, 3, 50, 1, 1)]
	private int Vshort = 10;

	[InputParameter("Volatility long", 3, 20, 500, 1, 1)]
	private int Vlong = 65;

	[InputParameter("Phase", 4, -100, 100, 1, 2)]
	private double Jphase = 0.0;

	#endregion Parameters

	private TBars bars;

	///////
	private JMA_Series indicator;
	///////

	public JMA_chart() {
		this.SeparateWindow = false;
		this.Name = "JMA - Jurik Moving Avg";
		this.Description = "Jurik Moving Average description";
		this.AddLineSeries("JMA", Color.Yellow, 3, LineStyle.Solid);
	}


	protected override void OnInit() {
		this.bars = new();
		this.indicator = new(source: bars.Select(this.DataSource), period: this.Period, phase: Jphase, vshort: Vshort, vlong: Vlong, useNaN: false);
	}

	protected override void OnUpdate(UpdateArgs args) {
		bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);

		this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
									this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low),
									this.GetPrice(PriceType.Close), this.GetPrice(PriceType.Volume), update);
		double result = this.indicator[this.indicator.Count - 1].v;

		this.SetValue(result, lineIndex: 0);
	}
}
