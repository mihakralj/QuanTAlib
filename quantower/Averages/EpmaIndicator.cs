using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class EpmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Epma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"EPMA {Period} : {SourceName}";

    public EpmaIndicator() : base()
    {
        Name = "EPMA - Endpoint Moving Average";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Epma(period: Period);
    }
}
