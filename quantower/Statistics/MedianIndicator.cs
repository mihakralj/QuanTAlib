using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MedianIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 1000, 1, 0)]
    public int Periods { get; set; } = 20;

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

    private Median? med;
    protected LineSeries? MedianSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Periods;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public MedianIndicator()
    {
        Name = "Median";
        Description = "Calculates the median value over a specified period";
        SeparateWindow = false;
        SourceName = Source.ToString();

        MedianSeries = new("Median", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(MedianSeries);
    }

    protected override void OnInit()
    {
        med = new Median(Periods);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = med!.Calc(input);

        MedianSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Median ({Periods}:{SourceName})";
}
