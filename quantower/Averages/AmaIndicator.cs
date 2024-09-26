using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class AmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Alpha", sortIndex: 2, minimum: -0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double Alpha { get; set; } = 0.1;
    private Ama? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"AMA {Period} : {Alpha} : {SourceName}";


    public AmaIndicator()
    {
        Name = "AMA - Adaptive Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Ama(period: Period, alpha: Alpha);
    }
}
