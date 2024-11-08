using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class PmoIndicator : Indicator
{
    [InputParameter("First Period", sortIndex: 1, minimum: 1, maximum: 2000, increment: 1)]
    public int Period1 { get; set; } = 35;

    [InputParameter("Second Period", sortIndex: 2, minimum: 1, maximum: 2000, increment: 1)]
    public int Period2 { get; set; } = 20;

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

    private Pmo? pmo;
    protected LineSeries? Series;
    protected string? SourceName;

    public override string ShortName => $"PMO({Period1},{Period2})";

    public PmoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "PMO - Price Momentum Oscillator";
        Description = "A momentum indicator that uses exponential moving averages of ROC to identify overbought and oversold conditions";

        Series = new(name: $"PMO({Period1},{Period2})", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        pmo = new Pmo(period1: Period1, period2: Period2);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = pmo!.Calc(input);

        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, pmo!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
