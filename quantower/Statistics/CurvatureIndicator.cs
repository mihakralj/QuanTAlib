using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class CurvatureIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    private Curvature? curvature;
    protected override AbstractBase QuanTAlib => curvature!;
    public override string ShortName => $"CURVATURE {Period} : {SourceName}";

    public CurvatureIndicator()
    {
        Name = "CURVATURE - Rate of Change of Slope";
        Description = "Measures the rate of change of the slope, indicating acceleration or deceleration in price movement.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        curvature = new(Period);
        MinHistoryDepths = curvature.WarmupPeriod;
    }
}
