using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class DemaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;
    private Dema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"DEMA {Period} : {SourceName}";

    public DemaIndicator() : base()
    {
        Name = "DEMA - Double Exponential Moving Average";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Dema(period: Period);
    }
}
