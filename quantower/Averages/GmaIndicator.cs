using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class GmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Gma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"GMA {Period} : {SourceName}";


    public GmaIndicator() : base()
    {
        Name = "GMA - Gaussian-Weighted Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Gma(Period);
        base.InitIndicator();
    }
}
