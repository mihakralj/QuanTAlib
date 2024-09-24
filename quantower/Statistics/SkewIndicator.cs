
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class SkewIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 3, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    private Skew? skew;
    protected override AbstractBase QuanTAlib => skew!;
    public override string ShortName => $"SKEW {Period} : {SourceName}";

    public SkewIndicator()
    {
        Name = "SKEW - Skewness";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        skew = new(Period);
        MinHistoryDepths = skew.WarmupPeriod;
    }
}