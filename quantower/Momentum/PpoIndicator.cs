using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class PpoIndicator : Indicator
{
    [InputParameter("Fast Period", sortIndex: 1, minimum: 1, maximum: 2000, increment: 1)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, minimum: 1, maximum: 2000, increment: 1)]
    public int SlowPeriod { get; set; } = 26;

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

    private Ppo? ppo;
    protected LineSeries? Series;
    protected string? SourceName;

    public override string ShortName => $"PPO({FastPeriod},{SlowPeriod})";

    public PpoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "PPO - Percentage Price Oscillator";
        Description = "A momentum indicator that shows the percentage difference between two moving averages";

        Series = new(name: $"PPO({FastPeriod},{SlowPeriod})", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        if (FastPeriod >= SlowPeriod)
        {
            FastPeriod = 12;
            SlowPeriod = 26;
        }
        ppo = new Ppo(fastPeriod: FastPeriod, slowPeriod: SlowPeriod);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ppo!.Calc(input);

        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, ppo!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
