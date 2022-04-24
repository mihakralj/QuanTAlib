using System.Drawing;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MIN_chart : Indicator
{
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private int Period = 10;

    [InputParameter("Data source", 1, variants: new object[]
      { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
      "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
    private int DataSource = 2;

    #endregion Parameters

    private TBars bars;

    ///////
    private MIN_Series indicator;
    ///////

    public MIN_chart()
    {
        this.SeparateWindow = false;
        this.Name = "MIN - Moving Minimum";
        this.Description = "Moving Minimum description";
        this.AddLineSeries("MIN", Color.RoyalBlue, 3, LineStyle.Solid);
    }

    protected override void OnInit()
    {
	    this.bars = new();
	    this.ShortName =
            "MIN (" + TBars.SelectStr(this.DataSource) + ", " + this.Period + ")";
        this.indicator =
            new(source: bars.Select(this.DataSource), period: this.Period);
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

        this.SetValue(result, 0);
    }
}
