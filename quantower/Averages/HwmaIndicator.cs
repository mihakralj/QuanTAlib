using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class HwmaIndicator : IndicatorBase
{
    [InputParameter("nA - smoothed series", sortIndex: 5, minimum: 0.0, maximum: 1.0, increment: 0.1, decimalPlaces: 2)]
    public double nA { get; set; } = 0.18;

    [InputParameter("nB - assess the trend (from 0 to 1)", sortIndex: 6, minimum: 0.0, maximum: 1.0, increment: 0.1, decimalPlaces: 2)]
    public double nB { get; set; } = 0.1;

    [InputParameter("nC - assess seasonality (from 0 to 1)", sortIndex: 7, minimum: 0.0, maximum: 1.0, increment: 0.1, decimalPlaces: 2)]
    public double nC { get; set; } = 0.1;

    private Hwma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"HWMA {nA:F2} : {nB:F2} : {nC:F2} : {SourceName}";


    public HwmaIndicator()
    {
        Name = "HWMA - Holt-Winter Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Hwma(nA: nA, nB: nB, nC: nC);
        base.InitIndicator();
    }
}
