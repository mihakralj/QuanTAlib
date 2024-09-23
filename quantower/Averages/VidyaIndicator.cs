using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class VidyaIndicator : IndicatorBase
{
    [InputParameter("Short Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;
    [InputParameter("Long Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int LPeriod { get; set; } = 40;
    [InputParameter("Alpha", sortIndex: 3, 0, 1, 0.1, 1)]
    public double Alpha { get; set; } = 0.4;

    private Vidya? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"VIDYA {Period} : {SourceName}";


    public VidyaIndicator() : base()
    {
        Name = "VIDYA - Variable Index Dynamic Average";
    }

    protected override void InitIndicator()
    {
        ma = new Vidya(Period, LPeriod, Alpha);
        base.InitIndicator();
    }
}
