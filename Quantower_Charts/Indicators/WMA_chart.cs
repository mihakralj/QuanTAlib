using System.Drawing;
using TradingPlatform.BusinessLayer;
using QuantLib;

public class WMA_chart : Indicator
{
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private int Period = 10;

    [InputParameter("Data source", 1, variants: new object[]{
            "Open", 0,
            "High", 1,
            "Low", 2,
            "Close", 3,
            "HL2", 4,
            "OC2", 5,
            "OHL3", 6,
            "HLC3", 7,
            "OHLC4", 8,
            "Weighted (HLCC4)", 9
        })]
    private int DataSource = 3;

    #endregion Parameters

    private readonly TBars bars = new();

    ///////
    private WMA_Series indicator;
    ///////

    public WMA_chart()
    {
        this.SeparateWindow = false;
        this.Name = "WMA - Weighted Moving Average";
        this.Description = "Weighted Moving Average description";
        this.AddLineSeries("WMA", Color.RoyalBlue, 3, LineStyle.Solid);
    }

    protected override void OnInit()
    {
        this.ShortName = "WMA (" + TBars.SelectStr(this.DataSource) + ", " + this.Period + ")";
        this.indicator = new(source: bars.Select(this.DataSource), period: this.Period, useNaN: false);
    }

    protected void OnNewData(bool update = false)
    { this.indicator.Add(update); }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.GetPrice(PriceType.Open), this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low), this.GetPrice(PriceType.Close), this.GetPrice(PriceType.Volume), update);
        this.OnNewData(update);

        double result = this.indicator[this.indicator.Count - 1].v;
        this.SetValue(result);
    }
}
