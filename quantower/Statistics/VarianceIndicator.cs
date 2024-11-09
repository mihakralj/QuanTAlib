using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class VarianceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Population", sortIndex: 2)]
    public bool IsPopulation { get; set; } = false;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Variance? variance;
    protected LineSeries? VarianceSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public VarianceIndicator()
    {
        Name = "Variance";
        Description = "Measures the spread of a set of numbers from their average value";
        SeparateWindow = true;
        SourceName = Source.ToString();

        VarianceSeries = new("Variance", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(VarianceSeries);
    }

    protected override void OnInit()
    {
        variance = new Variance(Period, IsPopulation);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = variance!.Calc(input);

        VarianceSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Variance ({Period}, {(IsPopulation ? "Pop" : "Sample")}:{SourceName})";
}
