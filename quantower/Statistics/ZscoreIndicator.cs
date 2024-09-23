using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class ZScoreIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    private Zscore? zScore;
    protected override AbstractBase QuanTAlib => zScore!;
    public override string ShortName => $"ZSCORE {Period} : {SourceName}";

    public ZScoreIndicator() : base()
    {
        Name = "ZSCORE - Standard Score";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        zScore = new(Period);
        MinHistoryDepths = zScore.WarmupPeriod;
        base.InitIndicator();
    }

}