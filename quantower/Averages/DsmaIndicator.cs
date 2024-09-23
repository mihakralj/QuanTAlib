using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class DsmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Dsma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"DSMA {Period} : {SourceName}";

    public DsmaIndicator() : base()
    {
        Name = "DSMA - Deviation Scaled Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Dsma(Period);
        MinHistoryDepths = ma.WarmupPeriod;
        base.InitIndicator();
    }
}
