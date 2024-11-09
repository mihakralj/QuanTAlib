using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AdxrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adxr? adxr;
    protected LineSeries? AdxrSeries;
    public int MinHistoryDepths => Math.Max(5, Period * 4); // Need extra periods for ADXR calculation
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public AdxrIndicator()
    {
        Name = "ADXR - Average Directional Movement Index Rating";
        Description = "Measures trend strength by comparing current ADX with historical ADX values.";
        SeparateWindow = true;

        AdxrSeries = new($"ADXR {Period}", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(AdxrSeries);
    }

    protected override void OnInit()
    {
        adxr = new Adxr(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = adxr!.Calc(input);

        AdxrSeries!.SetValue(result.Value);
        AdxrSeries!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"ADXR ({Period})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintHLine(args, 25, new Pen(color: IndicatorExtensions.Momentum, width: 1)); // Strong trend line
        this.PaintHLine(args, 20, new Pen(color: IndicatorExtensions.Momentum, width: 1)); // Weak trend line
        this.PaintSmoothCurve(args, AdxrSeries!, adxr!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
