using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class T3Indicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Vfactor", sortIndex: 2, 0, 1, 0.01, 2)]
    public double Vfactor { get; set; } = 0.62;

    [InputParameter("Use SMA for warmup", sortIndex: 3)]
    public bool UseSma { get; set; }

    private T3? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"T3 {Period} : {Vfactor:F2} : {SourceName}";

    public T3Indicator()
    {
        Name = "T3 - Tillson T3 Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new T3(period: Period, vfactor: Vfactor, useSma: UseSma);
    }
}
