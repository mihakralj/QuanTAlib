using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class SkewIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 3, 1000, 1, 0)]
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

    private Skew? skew;
    protected LineSeries? SkewSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 3;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public SkewIndicator()
    {
        Name = "Skew";
        Description = "Measures the asymmetry of the probability distribution of a real-valued random variable about its mean";
        SeparateWindow = true;
        SourceName = Source.ToString();

        SkewSeries = new("Skew", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(SkewSeries);
    }

    protected override void OnInit()
    {
        skew = new Skew(Periods);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = skew!.Calc(input);

        SkewSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Skew ({Periods}:{SourceName})";
}
