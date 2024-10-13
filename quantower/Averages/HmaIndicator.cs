using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 2, 1000, 1, 0)]
    public int Periods { get; set; } = 14;

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

    private Hma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Periods + (int)Math.Sqrt(Periods) - 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public HmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HMA - Hull Moving Average";
        Description = "Hull Moving Average";
        Series = new(name: $"HMA {Periods}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Hma(Periods);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"HMA {Periods}:{SourceName}";
}
