using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class PwmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Pwma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"PWMA {Period} : {SourceName}";

    public PwmaIndicator() : base()
    {
        Name = "PWMA - Pascal's Weighted Moving Average";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Pwma(period: Period);
    }
}
