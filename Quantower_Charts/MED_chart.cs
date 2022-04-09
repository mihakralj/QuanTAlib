using System.Drawing;
using TradingPlatform.BusinessLayer;
using QuantLib;

public class MINMAXMED_chart : Indicator
{
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private int Period = 9;

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

    private readonly QuantLib.TBars bars = new();

    ///////
    private QuantLib.MED_Series indicator;
    private QuantLib.MIN_Series mmin;
    private QuantLib.MAX_Series mmax;
    ///////

public MINMAXMED_chart()
    {
        this.SeparateWindow = false;
        this.Name = "MEDIAN - Movin Median, Minimum and Maximum";
        this.Description = "Moving Median/Minimum/Maximum description";
        this.AddLineSeries("MED", Color.SkyBlue, 3, LineStyle.Solid);
        this.AddLineSeries("MIN", Color.Yellow, 1, LineStyle.Solid);
        this.AddLineSeries("MAX", Color.Yellow, 1, LineStyle.Solid);
}

    protected override void OnInit()
    {
        this.ShortName = "MED (" + bars.SelectStr(this.DataSource) + ", " + this.Period + ")";
        this.indicator = new(source: bars.Select(this.DataSource), period: this.Period);
        this.mmin = new(source: bars.Select(this.DataSource), period: this.Period);
        this.mmax = new(source: bars.Select(this.DataSource), period: this.Period);
}

    protected void OnNewData(bool update = false)
    { 
        this.indicator.Add(update);
        this.mmin.Add(update);
        this.mmax.Add(update);
}

    protected override void OnUpdate(UpdateArgs args)
    {
        bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);
        this.bars.Add(this.Time(), this.GetPrice(PriceType.Open), this.GetPrice(PriceType.High), this.GetPrice(PriceType.Low), this.GetPrice(PriceType.Close), this.GetPrice(PriceType.Volume), update);
        this.OnNewData(update);

        double result = this.indicator[this.indicator.Count - 1].v;
        double minresult = this.mmin[this.mmin.Count - 1].v;
        double maxresult = this.mmax[this.mmax.Count - 1].v;

        this.SetValue(result,0);
        this.SetValue(minresult, 1);
        this.SetValue(maxresult, 2);



}
}
