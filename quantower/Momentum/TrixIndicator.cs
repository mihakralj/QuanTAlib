using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class TrixIndicator : Indicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 1, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 18;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Trix? trix;
    protected LineSeries? Series;
    protected string? SourceName;

    public override string ShortName => $"TRIX({Period})";

    public TrixIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "TRIX - Triple Exponential Average Rate of Change";
        Description = "A momentum oscillator that shows the percentage rate of change of a triple exponentially smoothed moving average";

        Series = new(name: $"TRIX({Period})", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        trix = new Trix(period: Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = trix!.Calc(input);

        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, trix!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
