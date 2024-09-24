using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class AlmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Offset", sortIndex: 5)]
    public double Offset { get; set; } = 0.85;

    [InputParameter("Sigma", sortIndex: 6)]
    public double Sigma { get; set; } = 6.0;
    private Alma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"ALMA {Period} : {Offset:F2} : {Sigma:F0} : {SourceName}";

    public AlmaIndicator()
    {
        Name = "ALMA - Arnaud Legoux Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Alma(period: Period, offset: Offset, sigma: Sigma);
    }
}
