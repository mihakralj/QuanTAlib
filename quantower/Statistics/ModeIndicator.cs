using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class ModeIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 50;

    private Mode? mode;
    protected override AbstractBase QuanTAlib => mode!;
    public override string ShortName => $"MODE {Period} : {SourceName}";
    public ModeIndicator() : base()
    {
        Name = "MODE - Most frequent historical value";
    }

    protected override void InitIndicator()
    {
        mode = new Mode(Period);
        MinHistoryDepths = mode.WarmupPeriod;
        base.InitIndicator();
    }
}