using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class VarianceIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, minimum: 2, maximum: 2000, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    private Variance? variance;
    protected override AbstractBase QuanTAlib => variance!;
    public override string ShortName => $"VAR {Period} : {SourceName}";
    public VarianceIndicator() : base()
    {
        Name = "VAR - Variance";
        Description = "Measures the spread of price data around its mean, indicating volatility and potential trend changes.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        SeparateWindow = true;
        variance = new(Period, IsPopulation);
        MinHistoryDepths = variance.WarmupPeriod;
        base.InitIndicator();
    }
}
