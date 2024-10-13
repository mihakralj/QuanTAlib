using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HistoricalIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 20;

    [InputParameter("Annualized", sortIndex: 2)]
    public bool IsAnnualized { get; set; } = true;

    private Historical? historical;
    protected LineSeries? HvSeries;
    public int MinHistoryDepths => Periods;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public HistoricalIndicator()
    {
        Name = "HV - Historical Volatility";
        Description = "Measures price fluctuations over time, indicating market volatility based on past price movements.";
        SeparateWindow = true;

        HvSeries = new("HV", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(HvSeries);
    }

    protected override void OnInit()
    {
        historical = new Historical(Periods, IsAnnualized);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = historical!.Calc(input);

        HvSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"HV ({Periods}{(IsAnnualized ? " - Annualized" : "")})";
}
