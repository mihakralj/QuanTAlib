using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MedianIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Median? med;
    protected LineSeries? MedianSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public MedianIndicator()
    {
        Name = "Median";
        Description = "Calculates the median value over a specified period";
        SeparateWindow = false;
        SourceName = Source.ToString();

        MedianSeries = new("Median", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(MedianSeries);
    }

    protected override void OnInit()
    {
        med = new Median(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = med!.Calc(input);

        MedianSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Median ({Period}:{SourceName})";
}
