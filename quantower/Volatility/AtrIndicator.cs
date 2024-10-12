using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class AtrIndicator : IndicatorBarBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    private Atr? atr;
    protected override AbstractBase QuanTAlib => atr!;
    public override string ShortName => $"ATR {Period}";
    public AtrIndicator()
    {
        Name = "ATR - Average True Range";
        Description = "Measures market volatility by calculating the average range between high and low prices.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        atr = new(Period);
        MinHistoryDepths = atr!.WarmupPeriod;
    }
}
