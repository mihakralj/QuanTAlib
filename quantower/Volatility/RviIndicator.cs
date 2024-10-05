using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class RviIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 2, 100, 1, 0)]
    public int Period { get; set; } = 10;

    private Rvi? rvi;
    protected override AbstractBase QuanTAlib => rvi!;
    public override string ShortName => $"RVI {Period} : {SourceName}";

    public RviIndicator() : base()
    {
        Name = "RVI - Relative Volatility Index";
        SeparateWindow = true;

        // Adding upper and lower reference lines
        //AddLineSeries("UpperLevel", 80, System.Drawing.Color.Gray, 1, LineStyle.Dot);
        //AddLineSeries("LowerLevel", 20, System.Drawing.Color.Gray, 1, LineStyle.Dot);
    }

    protected override void InitIndicator()
    {
        rvi = new Rvi(Period);
        MinHistoryDepths = rvi.WarmupPeriod;
        base.InitIndicator();
    }
}
