using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MaxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Decay", sortIndex: 2, 0, 10, 0.01, 2)]
    public double Decay { get; set; } = 0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.High;

    private Max? ma;
    protected LineSeries? MaxSeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public MaxIndicator()
    {
        Name = "Max";
        Description = "Calculates the maximum value over a specified period, with an optional decay factor";
        SeparateWindow = false;
        SourceName = Source.ToString();

        MaxSeries = new("Max", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(MaxSeries);
    }

    protected override void OnInit()
    {
        ma = new Max(Period, Decay);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        MaxSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Max ({Period}, {Decay:F2}:{SourceName})";
}
