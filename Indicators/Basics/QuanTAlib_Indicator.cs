using TradingPlatform.BusinessLayer;
using System.Drawing;
using QuanTAlib;
using System;
using TradingPlatform.BusinessLayer.Chart;

namespace QuanTAlib;

public class QuanTAlib_Indicator : Indicator {
	protected TBars bars;
	protected IChartWindow mainWindow;
	protected Graphics graphics;
	protected int firstOnScreenBarIndex, lastOnScreenBarIndex;

	protected override void OnInit() {
		base.OnInit();
		bars = new();
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