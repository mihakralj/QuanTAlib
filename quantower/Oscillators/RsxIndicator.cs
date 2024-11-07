using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class RsxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Rsi Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Data source", sortIndex: 5, variants: [
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

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rsx? rsx;
    protected string? SourceName;
    protected LineSeries? RsxSeries;
    public int MinHistoryDepths => Period + 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public RsxIndicator()
    {
        Name = "RSX - Jurik Trend Strengt Index";
        Description = "Measures the speed and magnitude of recent price changes to evaluate overbought or oversold conditions.";
        SeparateWindow = true;
        SourceName = Source.ToString();
        RsxSeries = new($"RSX {Period}", color: IndicatorExtensions.Oscillators, 2, LineStyle.Solid);
        AddLineSeries(RsxSeries);
    }

    protected override void OnInit()
    {
        rsx = new(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        rsx!.Calc(input);

        RsxSeries!.SetValue(rsx.Value);
        RsxSeries!.SetMarker(0, Color.Transparent);
    }

    public override string ShortName => $"RSX ({Period}:{SourceName})";

#pragma warning disable CA1416 // Validate platform compatibility
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, RsxSeries!, rsx!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
