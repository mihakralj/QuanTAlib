using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class PoIndicator : Indicator
{
    [InputParameter("Fast Period", sortIndex: 1, minimum: 1, maximum: 2000, increment: 1)]
    public int FastPeriod { get; set; } = 10;

    [InputParameter("Slow Period", sortIndex: 2, minimum: 1, maximum: 2000, increment: 1)]
    public int SlowPeriod { get; set; } = 21;

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

    private Po? po;
    protected LineSeries? Series;
    protected string? SourceName;

    public override string ShortName => $"PO({FastPeriod},{SlowPeriod})";

    public PoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "PO - Price Oscillator";
        Description = "A momentum indicator that measures the difference between two moving averages to identify price momentum";

        Series = new(name: $"PO({FastPeriod},{SlowPeriod})", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        if (FastPeriod >= SlowPeriod)
        {
            FastPeriod = 10;
            SlowPeriod = 21;
        }
        po = new Po(fastPeriod: FastPeriod, slowPeriod: SlowPeriod);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = po!.Calc(input);

        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, po!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
