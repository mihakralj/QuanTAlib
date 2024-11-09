using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HtitIndicator : Indicator, IWatchlistIndicator
{
    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Htit? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public static int MinHistoryDepths => 12; // Based on WarmupPeriod in Htit
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HTIT:{SourceName}";

    public HtitIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HTIT - Hilbert Transform Instantaneous Trendline";
        Description = "Hilbert Transform Instantaneous Trendline (Note: This indicator may not be fully functional)";
        Series = new(name: "HTIT", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Htit();
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
