using System.Collections;
using System.Drawing;
using System.Drawing.Text;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class ZLMA_chart : Indicator
{
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private int Period = 10;

    [InputParameter("Data source", 1, variants: new object[]
      { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
      "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int DataSource = 3;

    [InputParameter("MA algorithm", 2, variants: new object[]
    { "SMA", 0, 
	    "WMA", 1,  
	    "EMA", 2,  
	    "DEMA", 3,  
	    "TEMA", 4,  
	    "HMA", 5,
	    "KAMA", 6,  
	    "JMA", 7,
	    "SMMA", 8
			})]
    private int matype = 2;

#endregion Parameters

  private TBars bars;
	///////
  private TSeries indicator;
  ///////

  public ZLMA_chart()
    {
        this.SeparateWindow = false;
        this.Name = "ZLMA - Zero-lag Moving Average";
        this.Description = "Zero-Lag Moving Average description";
        this.AddLineSeries("ZLMA", Color.RoyalBlue, 3, LineStyle.Solid);
    }

  protected override void OnInit()
  {
	  this.bars = new();
	  string maname = matype switch
	  {
		  0 => "SMA",
		  1 => "WMA",
		  2 => "EMA",
		  3 => "DEMA",
		  4 => "TEMA",
		  5 => "HMA",
		  6 => "KAMA",
		  7 => "JMA",
			8 => "SMMA",
		  _ => "???"
	  };

	  this.ShortName = "ZLMA (" + maname + ", " + TBars.SelectStr(this.DataSource) + ", " + this.Period + ")";
	  this.zerolag = new(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
	  this.indicator = matype switch
	  {
		  0 => new SMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  1 => new WMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  2 => new EMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  3 => new DEMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  4 => new TEMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  5 => new HMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  6 => new KAMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  7 => new JMA_Series(source: zerolag, period: this.Period, useNaN: false),
		 8 => new SMMA_Series(source: zerolag, period: this.Period, useNaN: false),
		  _ => new EMA_Series(source: zerolag, period: this.Period, useNaN: false)
	  };
  }

  protected override void OnUpdate(UpdateArgs args)
    {
        bool update = !(args.Reason == UpdateReason.NewBar ||
                        args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
                      this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low),
                      this.GetPrice(PriceType.Close),
                      this.GetPrice(PriceType.Volume), update);

        double result = this.indicator[this.indicator.Count - 1].v;
        this.SetValue(result);
    }
}
