using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MaxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 1000, 1, 0)]
    public int Periods { get; set; } = 20;

    [InputParameter("Decay", sortIndex: 2, 0, 10, 0.01, 2)]
    public double Decay { get; set; } = 0;

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
    public SourceType Source { get; set; } = SourceType.High;

    private Max? ma;
    protected LineSeries? MaxSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public MaxIndicator()
    {
        Name = "Max";
        Description = "Calculates the maximum value over a specified period, with an optional decay factor";
        SeparateWindow = false;
        SourceName = Source.ToString();

        MaxSeries = new("Max", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(MaxSeries);
    }

    protected override void OnInit()
    {
        ma = new Max(Periods, Decay);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        MaxSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Max ({Periods}, {Decay:F2}:{SourceName})";
}
