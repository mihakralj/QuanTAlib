using TradingPlatform.BusinessLayer;
using QuanTAlib;

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
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Mma(period: Period);
    }
}
