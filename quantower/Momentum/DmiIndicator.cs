using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class DmiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dmi? dmi;
    protected LineSeries? PlusDiSeries;
    protected LineSeries? MinusDiSeries;
    public int MinHistoryDepths => Math.Max(5, Period * 2);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public DmiIndicator()
    {
        Name = "DMI - Directional Movement Index";
        Description = "Identifies the directional movement of a price by comparing successive highs and lows.";
        SeparateWindow = true;

        PlusDiSeries = new($"+DI {Period}", color: Color.Red, 2, LineStyle.Solid);
        MinusDiSeries = new($"-DI {Period}", color: Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(PlusDiSeries);
        AddLineSeries(MinusDiSeries);
    }

    protected override void OnInit()
    {
        dmi = new Dmi(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        dmi!.Calc(input);

        PlusDiSeries!.SetValue(dmi.PlusDI);
        MinusDiSeries!.SetValue(dmi.MinusDI);
        PlusDiSeries!.SetMarker(0, Color.Transparent);
        MinusDiSeries!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"DMI ({Period})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, PlusDiSeries!, dmi!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
        this.PaintSmoothCurve(args, MinusDiSeries!, dmi!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
