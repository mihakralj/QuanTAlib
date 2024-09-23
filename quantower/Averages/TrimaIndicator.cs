using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class TrimaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Trima? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"TRIMA {Period} : {SourceName}";


    public TrimaIndicator() : base()
    {
        Name = "TRIMA - Triangular Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Trima(Period);
        base.InitIndicator();
    }
}
