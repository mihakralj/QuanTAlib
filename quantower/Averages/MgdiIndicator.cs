using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MgdiIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("k Factor", sortIndex: 2, minimum: 0.0, maximum: 1.0, increment: 0.1, decimalPlaces: 2)]
    public double kfactor { get; set; } = 0.6;


    private Mgdi? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"MGDI {Period} : {kfactor:F2} : {SourceName}";


    public MgdiIndicator() : base()
    {
        Name = "MGDI - McGinley Dynamic Index";
    }

    protected override void InitIndicator()
    {
        ma = new Mgdi(period: Period, kFactor: kfactor);
        base.InitIndicator();
    }
}
