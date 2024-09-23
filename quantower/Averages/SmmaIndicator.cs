using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class SmmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Smma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"SMMA {Period} : {SourceName}";


    public SmmaIndicator() : base()
    {
        Name = "SMMA - Smoothed Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Smma(Period);
        base.InitIndicator();
    }
}
