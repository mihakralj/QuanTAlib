using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class StddevIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 2, 1000, 1, 0)]
    public int Periods { get; set; } = 20;

    [InputParameter("Population", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    [InputParameter("Data source", sortIndex: 3, variants: [
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

    private Stddev? stddev;
    protected LineSeries? StddevSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public StddevIndicator()
    {
        Name = "Standard Deviation";
        Description = "Measures the amount of variation or dispersion of a set of values";
        SeparateWindow = true;
        SourceName = Source.ToString();

        StddevSeries = new("StdDev", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(StddevSeries);
    }

    protected override void OnInit()
    {
        stddev = new Stddev(Periods, IsPopulation);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = stddev!.Calc(input);

        StddevSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"StdDev ({Periods}, {(IsPopulation ? "Pop" : "Sample")}:{SourceName})";
}
