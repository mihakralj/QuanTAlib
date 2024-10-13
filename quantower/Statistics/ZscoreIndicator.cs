using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ZscoreIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 2, 2000, 1, 0)]
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

    private Zscore? zScore;
    protected LineSeries? ZscoreSeries;
    protected string? SourceName;
    public int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public ZscoreIndicator()
    {
        Name = "Z-Score";
        Description = "Measures how many standard deviations a price is from the mean, indicating overbought/oversold levels.";
        SeparateWindow = true;
        SourceName = Source.ToString();

        ZscoreSeries = new("Z-Score", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(ZscoreSeries);
    }

    protected override void OnInit()
    {
        zScore = new Zscore(Periods);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = zScore!.Calc(input);

        ZscoreSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Z-Score ({Periods}:{SourceName})";
}
