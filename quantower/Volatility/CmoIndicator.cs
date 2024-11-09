using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class CmoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 9;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cmo? cmo;
    protected string? SourceName;
    protected LineSeries? CmoSeries;
    public int MinHistoryDepths => Period + 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;


    public CmoIndicator()
    {
        Name = "CMO - Chande Momentum Oscillator";
        Description = "Measures the momentum of price changes using the difference between the sum of recent gains and the sum of recent losses.";
        SeparateWindow = true;
        SourceName = Source.ToString();
        CmoSeries = new($"CMO {Period}", color: IndicatorExtensions.Volatility, 2, LineStyle.Solid);
        AddLineSeries(CmoSeries);
    }

    protected override void OnInit()
    {
        cmo = new Cmo(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        cmo!.Calc(input);

        CmoSeries!.SetValue(cmo.Value);
        CmoSeries!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override string ShortName => $"CMO ({Period}:{SourceName})";

#pragma warning disable CA1416 // Validate platform compatibility
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintHLine(args, 0, new Pen(Color.DarkGray, width: 1));
        this.PaintHLine(args, 50, new Pen(Color.Blue, width: 1));
        this.PaintHLine(args, -50, new Pen(Color.Blue, width: 1));
        this.PaintSmoothCurve(args, CmoSeries!, cmo!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
