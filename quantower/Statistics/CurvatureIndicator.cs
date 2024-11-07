using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class CurvatureIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 3, 1000, 1, 0)]
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

    private Curvature? curvature;
    protected LineSeries? CurvatureSeries;
    protected LineSeries? LineSeries;
    protected string? SourceName;
    public int MinHistoryDepths => (Periods * 2) - 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public CurvatureIndicator()
    {
        Name = "Curvature";
        Description = "Calculates the rate of change of the slope over a specified period";
        SeparateWindow = true;
        SourceName = Source.ToString();

        CurvatureSeries = new("Curvature", color: IndicatorExtensions.Statistics, 2, LineStyle.Solid);
        AddLineSeries(CurvatureSeries);
    }

    protected override void OnInit()
    {
        curvature = new Curvature(Periods);
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

    public override string ShortName => $"Curvature ({Periods}:{SourceName})";
}
