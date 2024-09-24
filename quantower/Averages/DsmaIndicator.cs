using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class DsmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;
    [InputParameter("Scale factor", sortIndex: 2, minimum: 0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double Scale { get; set; } = 0.5;

    private Dsma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"DSMA {Period} : {Scale:F2} : {SourceName}";

    public DsmaIndicator() : base()
    {
        Name = "DSMA - Deviation Scaled Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Dsma(Period, Scale);
        MinHistoryDepths = ma.WarmupPeriod;
        base.InitIndicator();
    }
}
