using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MamaIndicator : IndicatorBase
{
    [InputParameter("Fast limit", sortIndex: 2, 0, 1, 0.01, 2)]
    public double Fast { get; set; } = 0.4;
    [InputParameter("Slow limit", sortIndex: 3, 0, 1, 0.01, 2)]
    public double Slow { get; set; } = 0.04;
    private Mama? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"MAMA : {Fast} : {Slow} : {SourceName}";

    public MamaIndicator() : base()
    {
        Name = "MAMA - MESA Adaptive Moving Average";
        Description = "Adaptive moving average using MESA algorithm to adjust to market cycles and reduce lag.";
    }

    protected override void InitIndicator()
    {
        ma = new Mama(Fast, Slow);
        base.InitIndicator();
    }
}
