using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class HtitIndicator : IndicatorBase
{
    private Htit? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"HTIT : {SourceName}";

    public HtitIndicator() : base()
    {
        Name = "HTIT - Hilbert Transform Instantaneous Trendline";
    }

    protected override void InitIndicator()
    {
        ma = new Htit();
        MinHistoryDepths = ma.WarmupPeriod;
        base.InitIndicator();
    }
}
