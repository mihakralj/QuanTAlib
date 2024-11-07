using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ModeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 1000, 1, 0)]
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

    private Mode? mode;
    protected LineSeries? ModeSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Periods;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public ModeIndicator()
    {
        Name = "Mode";
        Description = "Calculates the most frequent value in a specified period";
        SeparateWindow = false;
        SourceName = Source.ToString();

        ModeSeries = new("Mode", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(ModeSeries);
    }

    protected override void OnInit()
    {
        mode = new Mode(Periods);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = mode!.Calc(input);

        ModeSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Mode ({Periods}:{SourceName})";
}
