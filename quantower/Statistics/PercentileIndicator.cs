using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class PercentileIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 2, 1000, 1, 0)]
    public int Periods { get; set; } = 20;

    [InputParameter("Percentile", sortIndex: 2, 0, 100, 0.1, 1)]
    public double PercentileValue { get; set; } = 50;

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

    private Percentile? percentile;
    protected LineSeries? PercentileSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public PercentileIndicator()
    {
        Name = "Percentile";
        Description = "Calculates the value at a specified percentile in a given period of data points";
        SeparateWindow = false;
        SourceName = Source.ToString();

        PercentileSeries = new("Percentile", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(PercentileSeries);
    }

    protected override void OnInit()
    {
        percentile = new Percentile(Periods, PercentileValue);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = percentile!.Calc(input);

        PercentileSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Percentile ({Periods}, {PercentileValue}%:{SourceName})";
}
