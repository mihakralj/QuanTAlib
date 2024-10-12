using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class SinemaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Sinema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"SINEMA {Period} : {SourceName}";

    public SinemaIndicator() : base()
    {
        Name = "SINEMA - Sine-Weighted Moving Average";
        Description = "Moving average using sine function for weighting, balancing recent and historical price data.";
    }

    protected override void InitIndicator()
    {
        ma = new Sinema(Period);
        base.InitIndicator();
    }
}
