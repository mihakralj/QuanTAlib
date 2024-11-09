using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class PercentileIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Percentile", sortIndex: 2, 0, 100, 0.1, 1)]
    public double PercentileValue { get; set; } = 50;

    [IndicatorExtensions.DataSourceInput]
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

        PercentileSeries = new("Percentile", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(PercentileSeries);
    }

    protected override void OnInit()
    {
        percentile = new Percentile(Period, PercentileValue);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = percentile!.Calc(input);

        PercentileSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Percentile ({Period}, {PercentileValue}%:{SourceName})";
}
