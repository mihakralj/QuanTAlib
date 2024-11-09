using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class VelIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 1, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 10;

    [InputParameter("Phase", sortIndex: 2, minimum: -100, maximum: 100, increment: 1)]
    public int Phase { get; set; } = 100;

    [InputParameter("Factor", sortIndex: 3, minimum: 0.1, maximum: 0.9, increment: 0.1, decimalPlaces: 2)]
    public double Factor { get; set; } = 0.25;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vel? vel;
    protected LineSeries? Series;
    protected LineSeries? ZeroLine;
    protected string? SourceName;
    public int MinHistoryDepths => Math.Max(5, Period * 2);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VEL({Period})";

    public VelIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "VEL - Velocity";
        Description = "An enhanced momentum indicator that applies JMA smoothing to momentum calculation";

        Series = new(name: $"VEL({Period})", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        ZeroLine = new("Zero", Color.Gray, 1, LineStyle.Dot);
        AddLineSeries(Series);
        AddLineSeries(ZeroLine);
    }

    protected override void OnInit()
    {
        vel = new Vel(period: Period, phase: Phase, factor: Factor);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewTick)
            return;

        TValue input = this.GetInputValue(args, Source);
        TValue result = vel!.Calc(input);

        Series!.SetValue(result.Value);
        ZeroLine!.SetValue(0);
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, vel!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
