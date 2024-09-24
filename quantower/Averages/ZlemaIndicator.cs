using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class ZlemaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Zlema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"ZLEMA {Period} : {SourceName}";


    public ZlemaIndicator() : base()
    {
        Name = "ZLEMA - Weighted Moving Average";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Zlema(Period);
    }
}
