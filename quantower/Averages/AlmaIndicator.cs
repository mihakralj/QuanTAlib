using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class AlmaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Offset", sortIndex: 5)]
    public double Offset = 0.85;

    [InputParameter("Sigma", sortIndex: 6)]
    public double Sigma = 6.0;
    private Alma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"ALMA {Period} : {Offset:F2} : {Sigma:F0} : {SourceName}";

    public AlmaIndicator() : base()
    {
        Name = "ALMA - Arnaud Legoux Moving Average";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Alma(period: Period, offset: Offset, sigma: Sigma);
    }
}
