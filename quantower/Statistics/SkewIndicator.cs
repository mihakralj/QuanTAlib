using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class SkewIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 3, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Skew? skew;
    protected LineSeries? SkewSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 3;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public SkewIndicator()
    {
        Name = "Skew";
        Description = "Measures the asymmetry of the probability distribution of a real-valued random variable about its mean";
        SeparateWindow = true;
        SourceName = Source.ToString();

        SkewSeries = new("Skew", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(SkewSeries);
    }

    protected override void OnInit()
    {
        skew = new Skew(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = skew!.Calc(input);

        SkewSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Skew ({Period}:{SourceName})";
}
