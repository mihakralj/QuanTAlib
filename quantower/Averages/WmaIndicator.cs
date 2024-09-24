using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class WmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Wma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"WMA {Period} : {SourceName}";


    public WmaIndicator() : base()
    {
        Name = "WMA - Weighted Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Wma(Period);
        base.InitIndicator();
    }
}
