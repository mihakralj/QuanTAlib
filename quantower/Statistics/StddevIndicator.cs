using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class StddevIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    private Stddev? stddev;
    protected override AbstractBase QuanTAlib => stddev!;
    public override string ShortName => $"STDDEV {Period} : {SourceName}";
    public StddevIndicator() : base()
    {
        Name = "STDDEV - Standard Deviation";
        Description = "Measures price volatility by calculating the dispersion of prices from their average over a period.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        stddev = new(Period, IsPopulation);
        MinHistoryDepths = stddev.WarmupPeriod;
        base.InitIndicator();
    }
}
