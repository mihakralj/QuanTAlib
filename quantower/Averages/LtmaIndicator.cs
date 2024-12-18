using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class LtmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Gamma", sortIndex: 1, 0.01, 1, 0.01, 2)]
    public double Gamma { get; set; } = 0.1;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ltma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public static int MinHistoryDepths => 4; // Based on WarmupPeriod in Ltma
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LTMA {Gamma}:{SourceName}";

    public LtmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "LTMA - Laguerre Time Moving Average";
        Description = "Laguerre Time Moving Average";
        Series = new(name: $"LTMA {Gamma}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Ltma(Gamma);
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
