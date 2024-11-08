using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class VortexIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vortex? vortex;
    protected LineSeries? ValueSeries;
    protected LineSeries? PlusLine;
    protected LineSeries? MinusLine;
    protected LineSeries? ZeroLine;
    public int MinHistoryDepths => Math.Max(5, Periods * 2);
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public VortexIndicator()
    {
        Name = "VORTEX - Vortex Indicator";
        Description = "A technical indicator consisting of two oscillating lines that identify trend reversals";
        SeparateWindow = true;

        ValueSeries = new($"VORTEX({Periods})", color: IndicatorExtensions.Momentum, 2, LineStyle.Solid);
        PlusLine = new($"VI+({Periods})", color: Color.Green, 2, LineStyle.Solid);
        MinusLine = new($"VI-({Periods})", color: Color.Red, 2, LineStyle.Solid);
        ZeroLine = new("Zero", Color.Gray, 1, LineStyle.Dot);

        AddLineSeries(ValueSeries);
        AddLineSeries(PlusLine);
        AddLineSeries(MinusLine);
        AddLineSeries(ZeroLine);
    }

    protected override void OnInit()
    {
        vortex = new Vortex(Periods);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        var result = vortex!.Calc(input);

        ValueSeries!.SetValue(result);
        PlusLine!.SetValue(vortex.ViPlus);
        MinusLine!.SetValue(vortex.ViMinus);
        ZeroLine!.SetValue(0);

        ValueSeries!.SetMarker(0, Color.Transparent);
        PlusLine!.SetMarker(0, Color.Transparent);
        MinusLine!.SetMarker(0, Color.Transparent);
    }

#pragma warning disable CA1416 // Validate platform compatibility

    public override string ShortName => $"VORTEX({Periods})";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, ValueSeries!, vortex!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
        this.PaintSmoothCurve(args, PlusLine!, vortex!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
        this.PaintSmoothCurve(args, MinusLine!, vortex!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
