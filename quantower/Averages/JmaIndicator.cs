using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class JmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Phase", sortIndex: 2, -100, 100, 1, 0)]
    public int Phase { get; set; } = 0;
    private Jma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"JMA {Period} : {Phase} : {SourceName}";

    public JmaIndicator() : base()
    {
        Name = "JMA - Jurik Moving Average";
        Description = "Adaptive moving average with reduced lag and noise, adjustable smoothness and phase shift.";
    }

    protected override void InitIndicator()
    {
        ma = new Jma(period: Period, phase: (double)Phase);
        base.InitIndicator();
    }
}
