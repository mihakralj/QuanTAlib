using System.Drawing;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class EntropyIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 2, 1000, 1, 0)]
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

    private Entropy? entropy;
    protected LineSeries? EntropySeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public EntropyIndicator()
    {
        Name = "Entropy";
        Description = "Measures the unpredictability of data using Shannon's Entropy";
        SeparateWindow = true;
        SourceName = Source.ToString();

        EntropySeries = new("Entropy", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(EntropySeries);
    }

    protected override void OnInit()
    {
        entropy = new Entropy(Periods);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = entropy!.Calc(input);

        EntropySeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Entropy ({Periods}:{SourceName})";
}
