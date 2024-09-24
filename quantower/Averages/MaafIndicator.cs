using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MaafIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 39;

    [InputParameter("Threshold", sortIndex: 5, minimum: 0, maximum: 1, increment: 0.001, decimalPlaces:3)]
    public double Threshold = 0.002;

    private Maaf? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"MAAF {Period} : {Threshold:F2} : {SourceName}";

    public MaafIndicator() : base()
    {
        Name = "MAAF - Median-Average Adaptive Filter";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Maaf(Period: Period, Threshold: Threshold);
    }
}
