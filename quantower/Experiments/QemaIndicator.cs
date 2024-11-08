using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class QemaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("K1", sortIndex: 1, 0.01, 1, 0.01, 2)]
    public double K1 { get; set; } = 0.2;

    [InputParameter("K2", sortIndex: 2, 0.01, 1, 0.01, 2)]
    public double K2 { get; set; } = 0.2;

    [InputParameter("K3", sortIndex: 3, 0.01, 1, 0.01, 2)]
    public double K3 { get; set; } = 0.2;

    [InputParameter("K4", sortIndex: 4, 0.01, 1, 0.01, 2)]
    public double K4 { get; set; } = 0.2;

    [InputParameter("Data source", sortIndex: 5, variants: [
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

    private Qema? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => (int)((2 - Math.Min(Math.Min(K1, K2), Math.Min(K3, K4))) / Math.Min(Math.Min(K1, K2), Math.Min(K3, K4)));
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"QEMA {K1},{K2},{K3},{K4}:{SourceName}";

    public QemaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "QEMA - Quadruple Exponential Moving Average";
        Description = "Quadruple Exponential Moving Average";
        Series = new(name: $"QEMA {K1},{K2},{K3},{K4}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Qema(K1, K2, K3, K4);
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
