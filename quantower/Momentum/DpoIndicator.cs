using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class DpoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 3)]
    public bool ShowColdValues { get; set; } = true;

    private Dpo? dpo;
    protected LineSeries? DpoSeries;
    public int MinHistoryDepths => Period * 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public DpoIndicator()
    {
        Name = "DPO - Detrended Price Oscillator";
        Description = "Removes trend from price by comparing current price to a past moving average, helping identify cycles in the price.";
        SeparateWindow = true;

        DpoSeries = new($"DPO {Period}", color: IndicatorExtensions.Momentum, 2, LineStyle.Solid);
        AddLineSeries(DpoSeries);
    }

    protected override void OnInit()
    {
        dpo = new Dpo(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = this.GetInputBar(args);
        TValue result = dpo!.Calc(input);

        DpoSeries!.SetValue(result.Value);
        DpoSeries!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"DPO ({Period})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, DpoSeries!, dpo!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
