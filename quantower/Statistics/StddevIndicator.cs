using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class StddevIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Stddev? stddev;
    protected LineSeries? StddevSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public StddevIndicator()
    {
        Name = "Standard Deviation";
        Description = "Measures the amount of variation or dispersion of a set of values";
        SeparateWindow = true;
        SourceName = Source.ToString();

        StddevSeries = new("StdDev", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(StddevSeries);
    }

    protected override void OnInit()
    {
        stddev = new Stddev(Period, IsPopulation);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = stddev!.Calc(input);

        StddevSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"StdDev ({Period}, {(IsPopulation ? "Pop" : "Sample")}:{SourceName})";
}
