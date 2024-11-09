using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class JmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Phase", sortIndex: 2, -100, 100, 1, 0)]
    public int Phase { get; set; } = 0;

    [InputParameter("Beta factor", sortIndex: 3, minimum: 0, maximum: 5, increment: 0.01, decimalPlaces: 2)]
    public double Factor { get; set; } = 0.45;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Jma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Math.Max(65, Period * 2);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"JMA {Period}:{Phase}:{Factor:F2}:{SourceName}";

    public JmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "JMA - Jurik Moving Average";
        Description = "Jurik Moving Average (Note: This indicator may have consistency issues)";
        Series = new(name: $"JMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Jma(period: Period, phase: Phase, factor: Factor);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, ma!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
