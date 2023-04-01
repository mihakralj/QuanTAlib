using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class DJMA_chart : Indicator {
	#region Parameters

	[InputParameter("Fast Data source", 0, variants: new object[]
		{ "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
			"OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
	private int FDataSource = 3;

	[InputParameter("Fast Smoothing period", 1, 1, 999, 1, 1)]
	private int FPeriod = 12;

	[InputParameter("Fast Volatility short", 2, 3, 50, 1, 1)]
	private int FVshort = 10;

	[InputParameter("Fast Volatility long", 3, 20, 500, 5, 1)]
	private int FVlong = 65;

	[InputParameter("Fast Phase", 4, -100, 100, 1, 2)]
	private double FJphase = 100.0;

	[InputParameter("Slow Data source", 5, variants: new object[]
		{ "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
			"OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
	private int SDataSource = 3;

	[InputParameter("Slow Smoothing period", 6, 1, 999, 1, 1)]
	private int SPeriod = 26;

	[InputParameter("Slow Volatility short", 7, 3, 50, 1, 1)]
	private int SVshort = 10;

	[InputParameter("Slow Volatility long", 8, 20, 500, 5, 1)]
	private int SVlong = 65;

	[InputParameter("Slow Phase", 9, -100, 100, 1, 2)]
	private double SJphase = -100.0;


	#endregion Parameters

	private TBars bars;

	///////
	private JMA_Series fJma, sJma;
	///////

	public DJMA_chart() {
		this.SeparateWindow = false;
		this.Name = "DJMA - Two JMAs";
		this.Description = "Jurik Moving Average description";
		this.AddLineSeries("JMA-fast", Color.Blue, 2, LineStyle.Solid);
		this.AddLineSeries("JMA-slow", Color.Green, 2, LineStyle.Solid);
	}


	protected override void OnInit() {
		this.bars = new();
		this.fJma = new(source: bars.Select(this.FDataSource), period: this.FPeriod, phase: FJphase, vshort: FVshort, vlong: FVlong, useNaN: false);
		this.sJma = new(source: bars.Select(this.SDataSource), period: this.SPeriod, phase: SJphase, vshort: SVshort, vlong: SVlong, useNaN: false);
	}

	protected override void OnUpdate(UpdateArgs args) {
		bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);

		this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
									this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low),
									this.GetPrice(PriceType.Close), this.GetPrice(PriceType.Volume), update);
		this.SetValue(this.fJma[^1].v, lineIndex: 0);
		this.SetValue(this.sJma[^1].v, lineIndex: 1);
	}
}