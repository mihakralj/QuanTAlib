using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class SmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Sma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"SMA {Period} : {SourceName}";


    public SmaIndicator() : base()
    {
        Name = "SMA - Simple Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Sma(Period);
        base.InitIndicator();
    }
}
