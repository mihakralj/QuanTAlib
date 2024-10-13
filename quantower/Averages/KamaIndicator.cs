using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class KamaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 1000, 1, 0)]
    public int Periods { get; set; } = 14;

    [InputParameter("Fast", sortIndex: 2, 1, 100, 1, 0)]
    public int Fast { get; set; } = 2;

    [InputParameter("Slow", sortIndex: 3, 1, 100, 1, 0)]
    public int Slow { get; set; } = 30;

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

    private Kama? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Periods;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public KamaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "KAMA - Kaufman's Adaptive Moving Average";
        Description = "Kaufman's Adaptive Moving Average";
        Series = new(name: $"KAMA {Periods}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Kama(Periods, Fast, Slow);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"KAMA {Periods}:{Fast}:{Slow}:{SourceName}";
}
