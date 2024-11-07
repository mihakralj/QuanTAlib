using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ObvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Obv? obv;
    protected LineSeries? ObvSeries;
    public int MinHistoryDepths => 5;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public ObvIndicator()
    {
        Name = "OBV - On-Balance Volume";
        Description = "Measures buying and selling pressure by analyzing volume in relation to price changes.";
        SeparateWindow = true;

        ObvSeries = new("OBV", color: IndicatorExtensions.Volume, 2, LineStyle.Solid);
        AddLineSeries(ObvSeries);
    }

    protected override void OnInit()
    {
        obv = new Obv();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = obv!.Calc(input);

        ObvSeries!.SetValue(result.Value);
        ObvSeries!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => "OBV";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintHLine(args, 0, new Pen(color: Color.DimGray, width: 1));
        this.PaintSmoothCurve(args, ObvSeries!, obv!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
