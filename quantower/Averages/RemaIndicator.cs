using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class RemaIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Regularization Factor", sortIndex: 2, minimum: 0, maximum: 2.5, increment: 0.1, decimalPlaces: 1)]
    public double Lambda { get; set; } = 0.5;

    private Rema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"REMA {Period} : {Lambda:F2} : {SourceName}";

    public RemaIndicator() : base()
    {
        Name = "REMA - Regularized Exponential Moving Average";
        Description = "EMA variant with regularization to reduce noise and improve stability in volatile markets.";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Rema(period: Period, lambda: Lambda);
    }
}
