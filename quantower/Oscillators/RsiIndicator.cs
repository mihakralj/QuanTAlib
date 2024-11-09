using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class RsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rsi? rsi;
    protected string? SourceName;
    protected LineSeries? RsiSeries;
    public int MinHistoryDepths => Period + 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public RsiIndicator()
    {
        Name = "RSI - Relative Strength Index";
        Description = "Measures the speed and magnitude of recent price changes to evaluate overbought or oversold conditions.";
        SeparateWindow = true;
        SourceName = Source.ToString();
        RsiSeries = new($"RSI {Period}", color: IndicatorExtensions.Oscillators, 2, LineStyle.Solid);
        AddLineSeries(RsiSeries);
    }

    protected override void OnInit()
    {
        rsi = new Rsi(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        rsi!.Calc(input);

        RsiSeries!.SetValue(rsi.Value);
        RsiSeries!.SetMarker(0, Color.Transparent);
    }

    public override string ShortName => $"RSI ({Period}:{SourceName})";

#pragma warning disable CA1416 // Validate platform compatibility
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, RsiSeries!, rsi!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
