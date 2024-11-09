using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class SlopeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 20;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Slope? slope;
    protected LineSeries? SlopeSeries;
    protected LineSeries? LineSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public SlopeIndicator()
    {
        Name = "Slope";
        Description = "Calculates the slope of a linear regression line for the specified period";
        SeparateWindow = true;
        SourceName = Source.ToString();

        SlopeSeries = new("Slope", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        LineSeries = new("Regression Line", Color.Red, 1, LineStyle.Solid);
        AddLineSeries(SlopeSeries);
        AddLineSeries(LineSeries);
    }

    protected override void OnInit()
    {
        slope = new Slope(Period);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = slope!.Calc(input);

        SlopeSeries!.SetValue(result.Value);
        if (slope.Line.HasValue)
        {
            LineSeries!.SetValue(slope.Line.Value);
        }
    }

    public override string ShortName
    {
        get
        {
            var result = $"Slope ({Period}:{SourceName})";
            if (slope != null)
            {
                result += $" Slope: {Math.Round(SlopeSeries!.GetValue(), 6)}";
                if (slope.Line.HasValue)
                    result += $", Line: {Math.Round(slope.Line.Value, 6)}";
                if (slope.Intercept.HasValue)
                    result += $", Intercept: {Math.Round(slope.Intercept.Value, 6)}";
                if (slope.RSquared.HasValue)
                    result += $", R²: {Math.Round(slope.RSquared.Value, 6)}";
            }
            return result;
        }
    }
}
