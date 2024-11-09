using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HwmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period (only when nA=nB=nC=0)", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("nA", sortIndex: 2, 0, 1, 0.01, 2)]
    public double NA { get; set; } = 0;

    [InputParameter("nB", sortIndex: 3, 0, 1, 0.01, 2)]
    public double NB { get; set; } = 0;

    [InputParameter("nC", sortIndex: 4, 0, 1, 0.01, 2)]
    public double NC { get; set; } = 0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Hwma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"HWMA {Period}:{NA}:{NB}:{NC}:{SourceName}";

    public HwmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HWMA - Holt-Winter Moving Average";
        Description = "Holt-Winter Moving Average";
        Series = new(name: $"HWMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        if ((NA, NB, NC) == (0, 0, 0))
        {
            ma = new Hwma(Period);
        }
        else
        {
            ma = new Hwma(Period, NA, NB, NC);
        }
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
