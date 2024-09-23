using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class KamaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Fast", sortIndex: 2, 1, 2000, 1, 0)]
    public int Fast { get; set; } = 2;
    [InputParameter("Slow", sortIndex: 3, 1, 2000, 1, 0)]
    public int Slow { get; set; } = 30;
    private Kama? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"KAMA {Period} : {Fast} : {Slow} : {SourceName}";


    public KamaIndicator() : base()
    {
        Name = "KAMA - Kaufman's Adaptive Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Kama(Period, Fast, Slow);
        base.InitIndicator();
    }
}
