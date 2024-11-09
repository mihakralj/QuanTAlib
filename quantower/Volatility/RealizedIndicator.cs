using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class RealizedIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Annualized", sortIndex: 2)]
    public bool IsAnnualized { get; set; } = true;

    private Rv? realized;
    protected LineSeries? RvSeries;
    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public RealizedIndicator()
    {
        Name = "RV - Realized Volatility";
        Description = "Measures actual price volatility over a specific period, useful for risk assessment and forecasting.";
        SeparateWindow = true;

        RvSeries = new("RV", color: IndicatorExtensions.Volatility, 2, LineStyle.Solid);
        AddLineSeries(RvSeries);
    }

    protected override void OnInit()
    {
        realized = new(Period, IsAnnualized);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = realized!.Calc(input);

        RvSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"RV ({Period}{(IsAnnualized ? " - Annualized" : "")})";
}
