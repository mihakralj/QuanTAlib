using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class SlopeIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    private Slope? slope;
    protected override AbstractBase QuanTAlib => slope!;
    public override string ShortName => $"SLOPE {Period} : {SourceName}";

    public SlopeIndicator()
    {
        Name = "SLOPE - Trend Slope";
        Description = "Measures the rate of change in price over a specified period, indicating trend strength and direction.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        slope = new(Period);
        MinHistoryDepths = slope.WarmupPeriod;
    }
}
