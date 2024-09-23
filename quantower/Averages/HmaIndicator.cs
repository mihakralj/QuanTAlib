using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class HmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Hma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"HMA {Period} : {SourceName}";


    public HmaIndicator() : base()
    {
        Name = "HMA - Hull Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Hma(Period);
        base.InitIndicator();
    }
}
