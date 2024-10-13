using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class HwmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 1000, 1, 0)]
    public int Periods { get; set; } = 14;

    [InputParameter("nA", sortIndex: 2, 0, 1, 0.01, 2)]
    public double NA { get; set; } = 0;

    [InputParameter("nB", sortIndex: 3, 0, 1, 0.01, 2)]
    public double NB { get; set; } = 0;

    [InputParameter("nC", sortIndex: 4, 0, 1, 0.01, 2)]
    public double NC { get; set; } = 0;

    [InputParameter("Data source", sortIndex: 5, variants: [
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

    private Hwma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Periods;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public HwmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "HWMA - Holt-Winter Moving Average";
        Description = "Holt-Winter Moving Average";
        Series = new(name: $"HWMA {Periods}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        if (NA == 0 && NB == 0 && NC == 0)
        {
            ma = new Hwma(Periods);
        }
        else
        {
            ma = new Hwma(Periods, NA, NB, NC);
        }
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"HWMA {Periods}:{NA}:{NB}:{NC}:{SourceName}";
}
