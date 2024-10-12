using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class DwmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Dwma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"DWMA {Period} : {SourceName}";

    public DwmaIndicator() : base()
    {
        Name = "DWMA - Double Weighted Moving Average";
        Description = "A moving average that applies double weighting to recent prices for increased responsiveness.";
    }

    protected override void InitIndicator()
    {
        ma = new Dwma(Period);
        base.InitIndicator();
    }
}
