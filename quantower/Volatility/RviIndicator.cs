using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class RviIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 100, 1, 0)]
    public int Period { get; set; } = 10;

    private Rvi? rvi;
    protected override AbstractBase QuanTAlib => rvi!;
    public override string ShortName => $"RVI {Period} : {SourceName}";

    public RviIndicator() : base()
    {
        Name = "RVI - Relative Volatility Index";
        Description = "Measures the direction of volatility, helping to identify overbought or oversold conditions in price.";
        SeparateWindow = true;
 }

    protected override void InitIndicator()
    {
        rvi = new Rvi(Period);
        MinHistoryDepths = rvi.WarmupPeriod;
        base.InitIndicator();
    }
}
