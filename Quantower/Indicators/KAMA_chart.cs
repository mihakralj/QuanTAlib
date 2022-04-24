using System.Diagnostics;
using System.Drawing;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class KAMA_chart : Indicator
{
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private int Period = 10;
    [InputParameter("Fastest EMA", 1, 1, 999, 1, 1)]
    private int Fast = 2;
    [InputParameter("Slowest EMA", 2, 1, 999, 1, 1)]
    private int Slow = 30;

  [InputParameter("Data source", 3, variants: new object[]
      { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
      "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int DataSource = 3;

    #endregion Parameters

    private TBars bars;

    ///////
    private KAMA_Series indicator;
    ///////

    public KAMA_chart()
    {
    this.SeparateWindow = false;
        this.Name = "KAMA - Kaufman's Adaptive Moving Average";
        this.Description = "Kaufman's Adaptive Moving Average description";
        this.AddLineSeries("KAMA", Color.RoyalBlue, 3, LineStyle.Solid);
    }

    protected override void OnInit()
    {
        this.ShortName = "KAMA (" + TBars.SelectStr(this.DataSource) + ", " + this.Period + ":" + this.Fast + ":" + this.Slow + ")";
      this.bars = new();
			this.indicator = new(source: bars.Select(this.DataSource), period: this.Period, fast: this.Fast, slow: this.Slow, useNaN: false);
		}

    protected override void OnUpdate(UpdateArgs args)
    {
        bool update = !(args.Reason == UpdateReason.NewBar ||
                        args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.GetPrice(PriceType.Open),
                      this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low),
                      this.GetPrice(PriceType.Close), this.GetPrice(PriceType.Volume), update);
        double result = this.indicator;
        this.SetValue(result);
		Debug.WriteLine($"{this.indicator[0].v}");
    }
}
