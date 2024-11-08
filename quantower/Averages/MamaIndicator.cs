using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MamaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Limit", sortIndex: 1, 0.01, 1, 0.01, 2)]
    public double FastLimit { get; set; } = 0.5;

    [InputParameter("Slow Limit", sortIndex: 2, 0.01, 1, 0.01, 2)]
    public double SlowLimit { get; set; } = 0.05;

    [InputParameter("Data source", sortIndex: 3, variants: [
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

    private Mama? ma;
    protected LineSeries? MamaSeries;
    protected LineSeries? FamaSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 6;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MAMA {FastLimit}:{SlowLimit}:{SourceName}";

    public MamaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "MAMA - MESA Adaptive Moving Average";
        Description = "MESA Adaptive Moving Average";
        MamaSeries = new(name: "MAMA", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        FamaSeries = new(name: "FAMA", color: Color.Red, width: 2, style: LineStyle.Solid);
        AddLineSeries(MamaSeries);
        AddLineSeries(FamaSeries);
    }

    protected override void OnInit()
    {
        ma = new Mama(FastLimit, SlowLimit);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        MamaSeries!.SetValue(result.Value);
        MamaSeries!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
        FamaSeries!.SetValue(ma.Fama.Value);
        FamaSeries!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, MamaSeries!, ma!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
        this.PaintSmoothCurve(args, FamaSeries!, ma!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
