using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class RmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Rma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"RMA {Period} : {SourceName}";

    public RmaIndicator() : base()
    {
        Name = "RMA - Wilder's Moving Average";
        Description = "Smoothed moving average that reduces whipsaws, commonly used in RSI calculations.";
    }

    protected override void InitIndicator()
    {
        ma = new Rma(Period);
        base.InitIndicator();
    }
}
