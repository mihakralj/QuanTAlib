using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class RealizedIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Annualized", sortIndex: 2)]
    public bool IsAnnualized { get; set; } = true;

    private Realized? realized;
    protected override AbstractBase QuanTAlib => realized!;
    public override string ShortName => $"Realized Volatility {Period}{(IsAnnualized ? " - Annualized" : "")} : {SourceName}";

    public RealizedIndicator() : base()
    {
        Name = "RV - Realized Volatility";
        Description = "Measures actual price volatility over a specific period, useful for risk assessment and forecasting.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        realized = new(Period, IsAnnualized);
        MinHistoryDepths = realized.WarmupPeriod;
        base.InitIndicator();
    }
}
