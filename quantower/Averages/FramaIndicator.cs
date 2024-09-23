using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class FramaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    private Frama? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"FRAMA {Period} : {SourceName}";


    public FramaIndicator() : base()
    {
        Name = "FRAMA - Fractal Adaptive Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Frama(Period);
        base.InitIndicator();
    }
}
