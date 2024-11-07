using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ApoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Data source", sortIndex: 4, variants: [
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

    private Apo? apo;
    protected LineSeries? ApoSeries;
    public int MinHistoryDepths => Math.Max(FastPeriod, SlowPeriod) * 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public ApoIndicator()
    {
        Name = "APO - Absolute Price Oscillator";
        Description = "Shows the difference between two moving averages of different periods.";
        SeparateWindow = true;

        ApoSeries = new($"APO {FastPeriod},{SlowPeriod}", color: IndicatorExtensions.Momentum, 2, LineStyle.Solid);
        AddLineSeries(ApoSeries);
    }

    protected override void OnInit()
    {
        apo = new Apo(FastPeriod, SlowPeriod);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = apo!.Calc(input);

        ApoSeries!.SetValue(result.Value);
        ApoSeries!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"APO ({FastPeriod},{SlowPeriod})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, ApoSeries!, apo!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
