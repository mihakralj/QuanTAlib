using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class AfirmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Alpha", sortIndex: 2, 0.01, 0.99, 0.01, 2)]
    public double Alpha { get; set; } = 0.1;
    private Afirma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"AFIRMA {Period} : {SourceName}";

    public AfirmaIndicator()
    {
        Name = "AFIRMA - Adaptive Filtering Integrated Recursive Moving Average";
        Description = "Adaptive Filtering Integrated Recursive Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Afirma(period: Period, alpha: Alpha);
    }
}