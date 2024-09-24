using TradingPlatform.BusinessLayer;
namespace QuanTAlib;
public class PercentileIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Percent", sortIndex: 2, 0, 100, 1, 0)]
    public double Percent { get; set; } = 50;

    private Percentile? percentile;
    protected override AbstractBase QuanTAlib => percentile!;
    public override string ShortName => $"PERCENTILE {Period} {Percent:F0}% : {SourceName}";

    public PercentileIndicator()
    {
        Name = "PERCENTILE - n-th Percentile ";
        SeparateWindow = false;
    }

    protected override void InitIndicator()
    {
        percentile = new(Period, Percent);
        MinHistoryDepths = percentile.WarmupPeriod;
    }

}