using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class CmoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 9;

    [InputParameter("Data source", sortIndex: 5, variants: [
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

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cmo? cmo;
    protected string? SourceName;
    protected LineSeries? CmoSeries;
    public int MinHistoryDepths => Periods + 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;


    public CmoIndicator()
    {
        Name = "CMO - Chande Momentum Oscillator";
        Description = "Measures the momentum of price changes using the difference between the sum of recent gains and the sum of recent losses.";
        SeparateWindow = true;
        SourceName = Source.ToString();
        CmoSeries = new($"CMO {Periods}", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(CmoSeries);
    }

    protected override void OnInit()
    {
        cmo = new Cmo(Periods);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        cmo!.Calc(input);

        CmoSeries!.SetValue(cmo.Value);
        CmoSeries!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override string ShortName => $"CMO ({Periods}:{SourceName})";

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
