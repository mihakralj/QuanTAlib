using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class TemaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Tema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"TEMA {Period} : {SourceName}";

    public TemaIndicator()
    {
        Name = "TEMA - Triple Exponential Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Tema(period: Period);
    }
}
