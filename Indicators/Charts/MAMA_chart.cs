using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MAMA_chart : Indicator {
	#region Parameters

	[InputParameter("Data source", 0, variants: new object[]
		{ "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
			"OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
	private int DataSource = 3;

	[InputParameter("Fastlimit", 1, 0, 1, 0.001, 5)]
	private double fastlimit = 0.5;

	[InputParameter("Slowlimit", 2, 0, 1, 0.001, 5)]
	private double slowlimit = 0.05;
	#endregion Parameters

	protected HistoricalData History;
	private TBars bars;
	///////
	private MAMA_Series indicator;
	///////

	public MAMA_chart() :base() {
		Name = "MAMA - MESA Adaptive Moving Average";
		AddLineSeries(lineName: "MAMA", lineColor: Color.Yellow, lineWidth:  3,lineStyle: LineStyle.Solid);
		SeparateWindow = false;
	}


	protected override void OnInit() {
		this.bars = new();

		this.History = this.Symbol.GetHistory(period: this.HistoricalData.Period, fromTime: HistoricalData.FromTime);
		for (int i = this.History.Count - 1; i >= 0; i--) {
			var rec = this.History[i, SeekOriginHistory.Begin];
			bars.Add(rec.TimeLeft, rec[PriceType.Open],
			rec[PriceType.High], rec[PriceType.Low],
			rec[PriceType.Close], rec[PriceType.Volume]);
		}
		indicator = new(source: bars.Select(DataSource),
										fastlimit: fastlimit, slowlimit: fastlimit,
										useNaN: true)
		;
	}

	protected override void OnUpdate(UpdateArgs args) {
		bool update = !(args.Reason == UpdateReason.NewBar ||
								args.Reason == UpdateReason.HistoricalBar);
		this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
									this.GetPrice(PriceType.High),
									this.GetPrice(PriceType.Low),
									this.GetPrice(PriceType.Close),
									this.GetPrice(PriceType.Volume), update);
		this.SetValue(indicator[^1].v, lineIndex: 0);
	}
}
