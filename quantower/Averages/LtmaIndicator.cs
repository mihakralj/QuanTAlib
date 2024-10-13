using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class LtmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Gamma", sortIndex: 1, 0.01, 1, 0.01, 2)]
    public double Gamma { get; set; } = 0.1;

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

    private Ltma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => 4; // Based on WarmupPeriod in Ltma
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public LtmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "LTMA - Laguerre Time Moving Average";
        Description = "Laguerre Time Moving Average";
        Series = new(name: $"LTMA {Gamma}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Ltma(Gamma);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"LTMA {Gamma}:{SourceName}";
}
