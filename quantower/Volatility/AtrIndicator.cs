using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AtrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 20;

    private Atr? atr;
    protected LineSeries? AtrSeries;
    public int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public AtrIndicator()
    {
        Name = "ATR - Average True Range";
        Description = "Measures market volatility by calculating the average range between high and low prices.";
        SeparateWindow = true;

        AtrSeries = new("ATR", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(AtrSeries);
    }

    protected override void OnInit()
    {
        atr = new Atr(Periods);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = atr!.Calc(input);

        AtrSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"ATR ({Periods})";
}
