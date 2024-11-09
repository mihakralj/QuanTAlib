using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MomIndicator : Indicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 1, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Mom? mom;
    protected LineSeries? Series;
    protected string? SourceName;

    public override string ShortName => $"MOM({Period})";

    public MomIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "MOM - Momentum";
        Description = "A basic momentum indicator that measures the change in price over a specified period";

        Series = new(name: $"MOM({Period})", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        mom = new Mom(period: Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = mom!.Calc(input);

        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, mom!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
