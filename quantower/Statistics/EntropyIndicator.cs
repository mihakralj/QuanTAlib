using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class EntropyIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 50;

    private Entropy? entropy;
    protected override AbstractBase QuanTAlib => entropy!;
    public override string ShortName => $"ENTROPY {Period} : {SourceName}";

    public EntropyIndicator() : base()
    {
        Name = "ENTROPY - Entropy";
        Description = "Measures the randomness or uncertainty in price movements, useful for identifying market phases.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        entropy = new(Period);
        MinHistoryDepths = entropy.WarmupPeriod;
        base.InitIndicator();
    }
}
