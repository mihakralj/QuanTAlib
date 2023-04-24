using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
namespace QuanTAlib;

public class JMA_chart : Indicator {
	#region Parameters

	[InputParameter("Data source", 0, variants: new object[]
		{ "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
			"OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
	private int DataSource = 3;

	[InputParameter("Smoothing period", 1, 1, 999, 1, 1)]
	private int Period = 9;

	[InputParameter("Volatility short", 2, 3, 50, 1, 1)]
	private int Vshort = 10;

	[InputParameter("Volatility long", 3, 20, 500, 1, 1)]
	private int Vlong = 65;

	[InputParameter("Phase", 4, -100, 100, 1, 2)]
	private double Jphase = 0.0;

	#endregion Parameters

	///////
	private JMA_Series indicator;
	///////

	protected TBars bars;
	protected IChartWindow mainWindow;
	protected Graphics graphics;
	protected int firstOnScreenBarIndex, lastOnScreenBarIndex;
	protected HistoricalData History;
	protected int HistPeriod;
	public JMA_chart() :base() {
		Name = "JMA - Jurik Moving Avg";
		Description = "Jurik Moving Average description";
		AddLineSeries(lineName: "JMA", lineColor: Color.Yellow, lineWidth:  3,lineStyle: LineStyle.Solid);
		SeparateWindow = false;
		HistPeriod = Period;
	}


	protected override void OnInit() { 
		base.OnInit();
		bars = new();
		var dur1 = this.HistoricalData.FromTime;
		var dur = this.HistoricalData.Period.Duration.TotalSeconds * (HistPeriod * 4); //seconds of two periods

		this.History = this.Symbol.GetHistory(period: this.HistoricalData.Period, fromTime: HistoricalData.FromTime);

		for (int i = this.History.Count - 1; i >= 0; i--) {

			var rec = this.History[i, SeekOriginHistory.Begin];

			bars.Add(rec.TimeLeft, rec[PriceType.Open],
				rec[PriceType.High], rec[PriceType.Low],
				rec[PriceType.Close], rec[PriceType.Volume]);
		}

		indicator = new(source: bars.Select(DataSource), period: Period, phase: Jphase, vshort: Vshort, vlong: Vlong, useNaN: true);
	}

	protected override void OnUpdate(UpdateArgs args) { 
		base.OnUpdate(args);
		bars.Add(Time(), GetPrice(PriceType.Open),
			GetPrice(PriceType.High),
			GetPrice(PriceType.Low),
			GetPrice(PriceType.Close),
			GetPrice(PriceType.Volume),
			update: !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar));
	
		this.SetValue(indicator[^1].v, lineIndex: 0);
	}
	public override void OnPaintChart(PaintChartEventArgs args) {
		base.OnPaintChart(args);
		if (this.CurrentChart == null)
			return;
		graphics = args.Graphics;
		mainWindow = this.CurrentChart.MainWindow;

		DateTime leftTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Left);
		DateTime rightTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Right);
		firstOnScreenBarIndex = (int)mainWindow.CoordinatesConverter.GetBarIndex(leftTime);
		lastOnScreenBarIndex = (int)Math.Ceiling(mainWindow.CoordinatesConverter.GetBarIndex(rightTime));
	}

}
