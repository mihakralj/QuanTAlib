using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class VidyaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Short Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int ShortPeriod { get; set; } = 14;

    [InputParameter("Long Period", sortIndex: 2, 0, 1000, 1, 0)]
    public int LongPeriod { get; set; } = 0;

    [InputParameter("Alpha", sortIndex: 3, 0.01, 1, 0.01, 2)]
    public double Alpha { get; set; } = 0.2;

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

    private Vidya? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => LongPeriod == 0 ? ShortPeriod * 4 : LongPeriod;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VIDYA {ShortPeriod}:{LongPeriod}:{Alpha}:{SourceName}";

    public VidyaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "VIDYA - Variable Index Dynamic Average";
        Description = "Variable Index Dynamic Average";
        Series = new(name: $"VIDYA {ShortPeriod}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Vidya(ShortPeriod, LongPeriod, Alpha);
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
        this.DrawText(args, Description);
    }
}
