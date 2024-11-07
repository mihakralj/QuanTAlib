using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class DpoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Data source", sortIndex: 2, variants: [
    "Open", SourceType.Open,
            "High", SourceType.High,
            "Low", SourceType.Low,
            "Close", SourceType.Close,
            "HL/2 (Median)", SourceType.HL2,
            "OC/2 (Midpoint)", SourceType.OC2,
            "OHL/3 (Mean)", SourceType.OHL3,
            "HLC/3 (Typical)", SourceType.HLC3,
            "OHLC/4 (Average)", SourceType.OHLC4,
            "HLCC/4 (Weighted)", SourceType.HLCC4
    ])]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 3)]
    public bool ShowColdValues { get; set; } = true;

    private Dpo? Dpo;
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
        Dpo = new Dpo(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = this.GetInputBar(args);
        TValue result = Dpo!.Calc(input);

        DpoSeries!.SetValue(result.Value);
        DpoSeries!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"DPO ({Period})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, DpoSeries!, Dpo!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
