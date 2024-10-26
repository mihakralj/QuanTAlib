using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class SlopeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 2, 1000, 1, 0)]
    public int Periods { get; set; } = 20;

    [InputParameter("Data source", sortIndex: 2, variants: [
        "Open", SourceType.Open,
        "High", SourceType.High,
        "Low", SourceType.Low,
        "Close", SourceType.Close,
        "HL/2 (Median)", SourceType.HL2,
        "OC/2 (Midpoint)", SourceType.OC2,
        "OHL/3 (Mean)", SourceType.OHL3,
        "HLC/3 (Typical)", SourceType.HLC3,
        "OHLC/4 (Average)", SourceType.OHLC4,
        "HLCC/4 (Weighted)", SourceType.HLCC4
    ])]
    public SourceType Source { get; set; } = SourceType.Close;

    private Slope? slope;
    protected LineSeries? SlopeSeries;
    protected LineSeries? LineSeries;
    protected string? SourceName;
    public int MinHistoryDepths => Periods;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public SlopeIndicator()
    {
        Name = "Slope";
        Description = "Calculates the slope of a linear regression line for the specified period";
        SeparateWindow = true;
        SourceName = Source.ToString();

        SlopeSeries = new("Slope", Color.Blue, 2, LineStyle.Solid);
        LineSeries = new("Regression Line", Color.Red, 1, LineStyle.Solid);
        AddLineSeries(SlopeSeries);
        AddLineSeries(LineSeries);
    }

    protected override void OnInit()
    {
        slope = new Slope(Periods);
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
            var result = $"Slope ({Periods}:{SourceName})";
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
