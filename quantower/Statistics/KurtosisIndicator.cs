using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class KurtosisIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 4, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    private Kurtosis? kurtosis;
    protected override AbstractBase QuanTAlib => kurtosis!;
    public override string ShortName => $"KURTOSIS {Period} : {SourceName}";

    public KurtosisIndicator() : base()
    {
        Name = "KURTOSIS - Relative Flatness";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        kurtosis = new(Period);
        MinHistoryDepths = kurtosis.WarmupPeriod;
        base.InitIndicator();
    }
}