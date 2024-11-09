using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class RviIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 100, 1, 0)]
    public int Period { get; set; } = 10;

    private Rvi? rvi;
    protected LineSeries? RviSeries;
    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public RviIndicator()
    {
        Name = "RVI - Relative Volatility Index";
        Description = "Measures the direction of volatility, helping to identify overbought or oversold conditions in price.";
        SeparateWindow = true;

        RviSeries = new("RVI", color: IndicatorExtensions.Volatility, 2, LineStyle.Solid);
        AddLineSeries(RviSeries);
    }

    protected override void OnInit()
    {
        rvi = new Rvi(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = rvi!.Calc(input);

        RviSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"RVI ({Period})";
}
