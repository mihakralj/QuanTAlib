using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AdxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adx? adx;
    protected LineSeries? AdxSeries;
    public int MinHistoryDepths => Math.Max(5, Periods * 3); // Need extra periods for ADX calculation
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public AdxIndicator()
    {
        Name = "ADX - Average Directional Movement Index";
        Description = "Measures the strength of a trend, regardless of its direction.";
        SeparateWindow = true;

        AdxSeries = new($"ADX {Periods}", color: IndicatorExtensions.Momentum, 2, LineStyle.Solid);
        AddLineSeries(AdxSeries);
    }

    protected override void OnInit()
    {
        adx = new Adx(Periods);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = adx!.Calc(input);

        AdxSeries!.SetValue(result.Value);
        AdxSeries!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"ADX ({Periods})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, AdxSeries!, adx!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
