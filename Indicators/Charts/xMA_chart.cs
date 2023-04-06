using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MovingAverage_chart : Indicator
{
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private int Period = 10;

    [InputParameter("Data source", 1, variants: new object[]
      { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
      "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int DataSource = 3;

	  [InputParameter("Moving Average Type", 2, variants: new object[]
		{ "SMA", 0, "EMA", 1,  "WMA", 2,  "T3", 3,  "SMMA", 4,  "TRIMA", 5, "DWMA", 6,  "FMA", 7,  "DEMA", 8,  "TEMA", 9,
			"ALMA", 10, "HMA", 11,  "HEMA", 12,  "MAMA", 13, "KAMA", 14, "ZLEMA", 15,  "JMA", 16})]
	  private int MAtype = 1;
	#endregion Parameters

	protected HistoricalData History;
	private TBars bars ;

    ///////
    private TSeries indicator;
      ///////

    public MovingAverage_chart()
    {
        this.SeparateWindow = false;
        this.Name = "Flexible Moving Average";
        this.AddLineSeries("MA", Color.Yellow, 3, LineStyle.Solid);
    }

    protected override void OnInit()
    {
      this.bars = new();
		  this.History = this.Symbol.GetHistory(period: this.HistoricalData.Period, fromTime: HistoricalData.FromTime);
		  for (int i = this.History.Count - 1; i >= 0; i--) {
				var rec = this.History[i, SeekOriginHistory.Begin];
				bars.Add(rec.TimeLeft, rec[PriceType.Open],
				rec[PriceType.High], rec[PriceType.Low],
				rec[PriceType.Close], rec[PriceType.Volume]);
			}

		switch (MAtype) {
			case 0:
				indicator = new SMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Simple Moving Average - SMA";
				break;
			case 1:
				indicator = new EMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Exponential Moving Average - EMA";
				break;
			case 2:
				indicator = new WMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Weighted Moving Average - WMA";
				break;
			case 3:
				indicator = new T3_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Tillson T3 Moving Average - T3";
				break;
			case 4:
				indicator = new SMMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Smoothed Moving Average - SMMA";
				break;
			case 5:
				indicator = new TRIMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Triangular Moving Average - TRIMA";
				break;
			case 6:
				indicator = new DWMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Double Weighted Moving Average - DWMA";
				break;
			case 7:
				indicator = new FMA_Series(source: bars.Select(this.DataSource), period: this.Period);
				this.Name = $"Fibonacci Moving Average - FMA";
				break;
			case 8:
				indicator = new DEMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Double Exponential Moving Average - DEMA";
				break;
			case 9:
				indicator = new TEMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Triple Exponential Moving Average - TEMA";
				break;
			case 10:
				indicator = new ALMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Arnaud Legoux Moving Average - ALMA";
				break;
			case 11:
				indicator = new HMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Hull Moving Average - HMA";
				break;
			case 12:
				indicator = new HEMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Hull-Exponential Moving Average - HEMA";
				break;
			case 13:
				double factor= 1.015 * Math.Exp(-0.043 * (double)this.Period);
				indicator = new MAMA_Series(source: bars.Select(this.DataSource), 
					fastlimit: factor, slowlimit: factor*0.1, 
					useNaN: false);
				this.Name = $"MESA Adaptive Moving Average - MAMA";
				break;
			case 14:
				indicator = new KAMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Kaufman's Adaptive Moving Average - KAMA";
				break;
			case 15:
				indicator = new ZLEMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Zero Lag Exponential Moving Average - ZLEMA";
				break;
			default:
				indicator = new JMA_Series(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
				this.Name = $"Jurik Moving Average - JMA";
				break;
		}
		this.Name = this.Name + $" ({Period}:{TBars.SelectStr(this.DataSource)})";
	}

    protected override void OnUpdate(UpdateArgs args)
    {
        bool update = !(args.Reason == UpdateReason.NewBar ||
                        args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
                      this.GetPrice(PriceType.High), 
                      this.GetPrice(PriceType.Low),
                      this.GetPrice(PriceType.Close),
                      this.GetPrice(PriceType.Volume), update);
        this.SetValue(this.indicator[this.indicator.Count - 1].v);
    }
}
