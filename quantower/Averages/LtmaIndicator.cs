using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class LtmaIndicator : IndicatorBase
{
    [InputParameter("Gamma", sortIndex: 1, 0, 1, 0.01, 2)]
    public double Gamma { get; set; } = 0.10;

    private Ltma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"Laguerre {Gamma:F2} : {SourceName}";

    public LtmaIndicator() : base()
    {
        Name = "LTMA - Laguerre Transform Moving Average";
    }

    protected override void InitIndicator()
    {
        ma = new Ltma(gamma: Gamma);
        base.InitIndicator();
    }
}
