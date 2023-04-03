using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class JMA_chart : QuanTAlib_Indicator {
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
	private EMA_Series indicator;
	///////

	public JMA_chart() :base() {
		Name = "JMA - Jurik Moving Avg";
		Description = "Jurik Moving Average description";
		AddLineSeries(lineName: "JMA", lineColor: Color.Yellow, lineWidth:  3,lineStyle: LineStyle.Solid);
		SeparateWindow = false;
		HistPeriod = Period;
	}


	protected override void OnInit() { 
		base.OnInit();
		indicator = new(source: bars.Select(DataSource), period: Period, 
		//								phase: Jphase, vshort: Vshort, vlong: Vlong, 
										useNaN: true);
	}

	protected override void OnUpdate(UpdateArgs args) { 
		base.OnUpdate(args);
		this.SetValue(indicator[^1].v, lineIndex: 0);
	}
}
