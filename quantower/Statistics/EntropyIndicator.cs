using System.Drawing;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class EntropyIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Entropy? entropy;
    protected LineSeries? EntropySeries;
    protected string? SourceName;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public EntropyIndicator()
    {
        Name = "Entropy";
        Description = "Measures the unpredictability of data using Shannon's Entropy";
        SeparateWindow = true;
        SourceName = Source.ToString();

        EntropySeries = new("Entropy", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(EntropySeries);
    }

    protected override void OnInit()
    {
        entropy = new Entropy(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = entropy!.Calc(input);

        EntropySeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Entropy ({Period}:{SourceName})";
}
