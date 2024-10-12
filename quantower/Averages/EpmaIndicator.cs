using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

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
        Description = "Moving average that emphasizes the most recent data point, useful for identifying trend changes.";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Epma(period: Period);
    }
}
