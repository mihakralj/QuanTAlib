using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AlmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Offset", sortIndex: 2, minimum: 0, maximum: 1, decimalPlaces: 2)]
    public double Offset { get; set; } = 0.85;

    [InputParameter("Sigma", sortIndex: 3, minimum: 0, maximum: 100, decimalPlaces: 1)]
    public double Sigma { get; set; } = 6.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Alma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ALMA {Period}:{Offset:F2}:{Sigma:F1}:{SourceName}";

    public AlmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "ALMA - Arnaud Legoux Moving Average";
        Description = "Arnaud Legoux Moving Average";
        Series = new(name: $"ALMA {Period}:{Offset:F2}:{Sigma:F0}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Alma(period: Period, offset: Offset, sigma: Sigma);
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
