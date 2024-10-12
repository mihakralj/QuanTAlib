using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class HistoricalIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Annualized", sortIndex: 2)]
    public bool IsAnnualized { get; set; } = true;

    private Historical? historical;
    protected override AbstractBase QuanTAlib => historical!;
    public override string ShortName => $"Historical Volatility {Period}{(IsAnnualized ? " - Annualized" : "")} : {SourceName}";

    public HistoricalIndicator() : base()
    {
        Name = "HV - Historical Volatility";
        Description = "Measures price fluctuations over time, indicating market volatility based on past price movements.";
        SeparateWindow = true;
    }

    protected override void InitIndicator()
    {
        historical = new(Period, IsAnnualized);
        MinHistoryDepths = historical.WarmupPeriod;
        base.InitIndicator();
    }
}
