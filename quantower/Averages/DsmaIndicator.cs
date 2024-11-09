using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class DsmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Scale factor", sortIndex: 2, minimum: 0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double Scale { get; set; } = 0.5;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dsma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths { get; private set; }
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DSMA {Period}:{Scale:F2}:{SourceName}";

    public DsmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "DSMA - Deviation Scaled Moving Average";
        Description = "A moving average that adjusts its responsiveness based on price deviations from the mean.";
        Series = new(name: $"DSMA {Period}:{Scale:F2}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Dsma(Period, Scale);
        MinHistoryDepths = ma.WarmupPeriod;
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
