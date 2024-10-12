using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class SmmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Smma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"SMMA {Period} : {SourceName}";

    public SmmaIndicator() : base()
    {
        Name = "SMMA - Smoothed Moving Average";
        Description = "Moving average that gives more weight to recent data while retaining all historical data.";
    }

    protected override void InitIndicator()
    {
        ma = new Smma(Period);
        base.InitIndicator();
    }
}
