using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class KurtosisIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 4, 1000, 1, 0)]
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

    private Kurtosis? kurtosis;
    protected LineSeries? KurtosisSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Periods - 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public KurtosisIndicator()
    {
        Name = "Kurtosis";
        Description = "Measures the 'tailedness' of the probability distribution of a real-valued random variable";
        SeparateWindow = true;
        SourceName = Source.ToString();

        KurtosisSeries = new("Kurtosis", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(KurtosisSeries);
    }

    protected override void OnInit()
    {
        kurtosis = new Kurtosis(Periods);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = kurtosis!.Calc(input);

        KurtosisSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Kurtosis ({Periods}:{SourceName})";
}
