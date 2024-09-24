using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class EntropyIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 50;

    private Entropy? entropy;
    protected override AbstractBase QuanTAlib => entropy!;
    public override string ShortName => $"ENTROPY {Period} : {SourceName}";

    public EntropyIndicator()
    {
        Name = "ENTROPY - Entropy";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        entropy = new(Period);
        MinHistoryDepths = entropy.WarmupPeriod;
    }
}