using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class RocIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 1, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 12;

    [InputParameter("Data source", sortIndex: 2, variants: [
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

    private Roc? roc;
    protected LineSeries? Series;
    protected LineSeries? ZeroLine;
    protected string? SourceName;
    public int MinHistoryDepths => Math.Max(5, Period * 2);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ROC({Period})";

    public RocIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "ROC - Rate of Change";
        Description = "A momentum indicator that measures the percentage change in price over a specified period";

        Series = new(name: $"ROC({Period})", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        ZeroLine = new("Zero", Color.Gray, 1, LineStyle.Dot);
        AddLineSeries(Series);
        AddLineSeries(ZeroLine);
    }

    protected override void OnInit()
    {
        roc = new Roc(period: Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewTick)
            return;

        TValue input = this.GetInputValue(args, Source);
        TValue result = roc!.Calc(input);

        Series!.SetValue(result.Value);
        ZeroLine!.SetValue(0);
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, roc!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
