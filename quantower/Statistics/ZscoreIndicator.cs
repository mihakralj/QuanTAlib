using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ZscoreIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 2000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Zscore? zScore;
    protected LineSeries? ZscoreSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public ZscoreIndicator()
    {
        Name = "Z-Score";
        Description = "Measures how many standard deviations a price is from the mean, indicating overbought/oversold levels.";
        SeparateWindow = true;
        SourceName = Source.ToString();

        ZscoreSeries = new("Z-Score", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(ZscoreSeries);
    }

    protected override void OnInit()
    {
        zScore = new Zscore(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = zScore!.Calc(input);

        ZscoreSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Z-Score ({Period}:{SourceName})";
}
