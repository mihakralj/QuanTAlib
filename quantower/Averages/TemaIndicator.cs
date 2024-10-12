using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class TemaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Tema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"TEMA {Period} : {SourceName}";

    public TemaIndicator() : base()
    {
        Name = "TEMA - Triple Exponential Moving Average";
        Description = "Moving average that applies EMA three times to reduce lag and improve responsiveness to trends.";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Tema(period: Period);
    }
}
