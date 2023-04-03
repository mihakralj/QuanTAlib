using TradingPlatform.BusinessLayer;
using System.Drawing;
using QuanTAlib;
using System;
using TradingPlatform.BusinessLayer.Chart;

namespace QuanTAlib;

public abstract class QuanTAlib_Indicator : Indicator {
	protected TBars bars;
	protected IChartWindow mainWindow;
	protected Graphics graphics;
	protected int firstOnScreenBarIndex, lastOnScreenBarIndex;
	protected HistoricalData History;
	protected int HistPeriod;

	protected override void OnInit() {
		base.OnInit();
		bars = new();
		var dur1 = this.HistoricalData.FromTime;
		var dur = this.HistoricalData.Period.Duration.TotalSeconds * (HistPeriod*4)  ; //seconds of two periods

		this.History = this.Symbol.GetHistory(period: this.HistoricalData.Period,  fromTime: HistoricalData.FromTime);

		for (int i = this.History.Count-1; i >= 0; i--) {

			var rec = this.History[i, SeekOriginHistory.Begin];

			bars.Add(rec.TimeLeft, rec[PriceType.Open], 
				 rec[PriceType.High], rec[PriceType.Low],
				 rec[PriceType.Close], rec[PriceType.Volume]);
		}
	}

	protected override void OnUpdate(UpdateArgs args) {
		base.OnUpdate(args);
		bars.Add(Time(), GetPrice(PriceType.Open),
									GetPrice(PriceType.High),
									GetPrice(PriceType.Low),
									GetPrice(PriceType.Close),
									GetPrice(PriceType.Volume),
									update: !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar));
	}
	public override void OnPaintChart(PaintChartEventArgs args) {
		base.OnPaintChart(args);
		if (this.CurrentChart == null) return;
		graphics = args.Graphics;
		mainWindow = this.CurrentChart.MainWindow;

		DateTime leftTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Left);
		DateTime rightTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Right);
		firstOnScreenBarIndex = (int)mainWindow.CoordinatesConverter.GetBarIndex(leftTime);
		lastOnScreenBarIndex = (int)Math.Ceiling(mainWindow.CoordinatesConverter.GetBarIndex(rightTime));
	}
}