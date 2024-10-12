using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Mma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"MMA {Period} : {SourceName}";

    public MmaIndicator() : base()
    {
        Name = "MMA - Modified Moving Average";
        Description = "Variation of EMA that reduces lag and smooths price action, balancing responsiveness and stability.";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Mma(period: Period);
    }
}
