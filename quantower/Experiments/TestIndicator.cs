using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class TestIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Data source", sortIndex: 20, variants: [
        "Open", SourceType.Open,
        "High", SourceType.High,
        "Low", SourceType.Low,
        "Close", SourceType.Close,
        "HL/2 (Median)", SourceType.HL2,
        "OC/2 (Midpoint)", SourceType.OC2,
        "OHL/3 (Mean)", SourceType.OHL3,
        "HLC/3 (Typical)", SourceType.HLC3,
        "OHLC/4 (Average)", SourceType.OHLC4,
        "HLCC/4 (Weighted)", SourceType.HLCC4
    ])]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Sma? ma;
    protected LineSeries? Series;
    public int MinHistoryDepths { get; set; }
    int IWatchlistIndicator.MinHistoryDepths => 0; //QuanTAlib indicators generate value immediately


    public TestIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "TEST";
        Description = "test and test and test and more test.";
        Series = new(name: $"{Name}", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Sma(Period);
        base.OnInit();
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
        Series!.SetValue(result);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, ma!.WarmupPeriod, ShowColdValues, tension: 0.2);
        this.DrawText(args, Description);
    }
}

