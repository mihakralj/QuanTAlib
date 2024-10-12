using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

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
        Description = "Adaptive moving average that adjusts its smoothing based on market fractal dimension.";
    }

    protected override void InitIndicator()
    {
        ma = new Frama(Period);
        base.InitIndicator();
    }
}
