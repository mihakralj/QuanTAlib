using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HtitIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Data source", sortIndex: 1, variants: [
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

    private Htit? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => 12; // Based on WarmupPeriod in Htit
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public HtitIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HTIT - Hilbert Transform Instantaneous Trendline";
        Description = "Hilbert Transform Instantaneous Trendline (Note: This indicator may not be fully functional)";
        Series = new(name: "HTIT", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Htit();
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"HTIT:{SourceName}";
}
