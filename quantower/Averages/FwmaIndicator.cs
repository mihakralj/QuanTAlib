using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class FwmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Fwma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"FWMA {Period} : {SourceName}";


    public FwmaIndicator() : base()
    {
        Name = "FWMA - Fibonacci-Weighted Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Fwma(Period);
        base.InitIndicator();
    }
}
