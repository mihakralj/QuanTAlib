using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MedianIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 50;

    private Median? med;
    protected override AbstractBase QuanTAlib => med!;
    public override string ShortName => $"MEDIAN {Period} : {SourceName}";
    public MedianIndicator() : base()
    {
        Name = "MEDIAN - Median historical value";
        Description = "Calculates the middle value of price data over a specified period, less affected by outliers than mean.";
    }

    protected override void InitIndicator()
    {
        med = new Median(Period);
        MinHistoryDepths = med.WarmupPeriod;
        base.InitIndicator();
    }
}
