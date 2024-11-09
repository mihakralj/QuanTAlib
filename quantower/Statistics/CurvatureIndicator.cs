using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class CurvatureIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 3, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Curvature? curvature;
    protected LineSeries? CurvatureSeries;
    protected LineSeries? LineSeries;
    protected string? SourceName;
    public int MinHistoryDepths => (Period * 2) - 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public CurvatureIndicator()
    {
        Name = "Curvature";
        Description = "Calculates the rate of change of the slope over a specified period";
        SeparateWindow = true;
        SourceName = Source.ToString();

        CurvatureSeries = new("Curvature", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        LineSeries = new("Line", color: Color.Red, 1, LineStyle.Solid);
        AddLineSeries(CurvatureSeries);
        AddLineSeries(LineSeries);
    }

    protected override void OnInit()
    {
        curvature = new Curvature(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = curvature!.Calc(input);

        CurvatureSeries!.SetValue(result.Value);
        if (curvature.Line.HasValue)
        {
            LineSeries!.SetValue(curvature.Line.Value);
        }
    }

    public override string ShortName => $"Curvature ({Period}:{SourceName})";
}
