using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class JmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 1000, 1, 0)]
    public int Periods { get; set; } = 10;

    [InputParameter("Phase", sortIndex: 2, -100, 100, 1, 0)]
    public int Phase { get; set; } = 0;

    [InputParameter("Beta factor", sortIndex: 3, minimum: 0, maximum: 5, increment: 0.01, decimalPlaces: 2)]
    public double Factor { get; set; } = 0.45;

    [InputParameter("Data source", sortIndex: 4, variants: [
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

    private Jma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Math.Max(65, Periods * 2);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"JMA {Periods}:{Phase}:{Factor:F2}:{SourceName}";

    public JmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "JMA - Jurik Moving Average";
        Description = "Jurik Moving Average (Note: This indicator may have consistency issues)";
        Series = new(name: $"JMA {Periods}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Jma(period: Periods, phase: Phase, factor: Factor);
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
