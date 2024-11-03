using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AtrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Atr? atr;
    protected LineSeries? AtrSeries;
    public int MinHistoryDepths => Math.Max(5, Periods * 2);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public AtrIndicator()
    {
        Name = "ATR - Average True Range";
        Description = "Measures market volatility by calculating the average range between high and low prices.";
        SeparateWindow = true;

        AtrSeries = new($"ATR {Periods}", Color.Blue, 2, LineStyle.Solid);
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
        AtrSeries!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }
#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"ATR ({Periods})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintHLine(args, 0.05, new Pen(Color.DarkRed, width: 2));
        this.PaintSmoothCurve(args, AtrSeries!, atr!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
