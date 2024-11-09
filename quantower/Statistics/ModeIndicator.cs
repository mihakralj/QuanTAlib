using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ModeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Mode? mode;
    protected LineSeries? ModeSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public ModeIndicator()
    {
        Name = "Mode";
        Description = "Calculates the most frequent value in a specified period";
        SeparateWindow = false;
        SourceName = Source.ToString();

        ModeSeries = new("Mode", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(ModeSeries);
    }

    protected override void OnInit()
    {
        mode = new Mode(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = mode!.Calc(input);

        ModeSeries!.SetValue(result.Value);
    }

    public override string ShortName => $"Mode ({Period}:{SourceName})";
}
