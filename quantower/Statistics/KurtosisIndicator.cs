using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class KurtosisIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 4, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Kurtosis? kurtosis;
    protected LineSeries? KurtosisSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Period - 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public KurtosisIndicator()
    {
        Name = "Kurtosis";
        Description = "Measures the 'tailedness' of the probability distribution of a real-valued random variable";
        SeparateWindow = true;
        SourceName = Source.ToString();

        KurtosisSeries = new("Kurtosis", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(KurtosisSeries);
    }

    protected override void OnInit()
    {
        kurtosis = new Kurtosis(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = kurtosis!.Calc(input);

        KurtosisSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Kurtosis ({Period}:{SourceName})";
}
