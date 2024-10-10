using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class EmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Use SMA for warmup", sortIndex: 5)]
    public bool UseSma { get; set; } = false;

    private Ema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"EMA {Period} : {SourceName}";

    public EmaIndicator()
    {
        Name = "EMA - Exponential Moving Average";
        Description = "Exponential Moving Average";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Ema(period: Period, useSma: UseSma);
    }
}
