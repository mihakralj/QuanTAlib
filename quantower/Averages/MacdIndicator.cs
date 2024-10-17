using System.Diagnostics.Metrics;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MacdIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Slow EMA", sortIndex: 1, 1, 1000, 1, 0)]
    public int Slow { get; set; } = 26;

    [InputParameter("Fast EMA", sortIndex: 2, 1, 2000, 1, 0)]
    public int Fast { get; set; } = 12;

    [InputParameter("Signal line", sortIndex: 3, 1, 2000, 1, 0)]
    public int Signal { get; set; } = 9;

    [InputParameter("Use SMA for warmup period", sortIndex: 2)]
    public bool UseSMA { get; set; } = false;

    [InputParameter("Data source", sortIndex: 3, variants: [
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

    private Ema? slow_ma;
    private Ema? fast_ma;
    private Ema? signal_ma;
    protected LineSeries? MainSeries;
        protected LineSeries? SignalSeries;

    protected string? SourceName;
    public int MinHistoryDepths => Slow;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MACD {Slow}:{Fast}:{Signal}";

    public MacdIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "MACD - Moving Average Convergence Divergence";
        Description = "MACD";
        MainSeries = new(name: $"MAIN", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        SignalSeries = new(name: $"SIGNAL", color: Color.Blue, width: 2, style: LineStyle.Solid);

        AddLineSeries(MainSeries);
        AddLineSeries(SignalSeries);
    }

    protected override void OnInit()
    {
        slow_ma = new(Slow, useSma: UseSMA);
        fast_ma = new(Fast, useSma: UseSMA);
        signal_ma = new(Signal, useSma: UseSMA);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        slow_ma!.Calc(input);
        fast_ma!.Calc(input);
        double main = fast_ma.Value - slow_ma.Value;
        signal_ma!.Calc(main);

        MainSeries!.SetValue(main);
        MainSeries!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
        SignalSeries!.SetValue(signal_ma.Value);
        SignalSeries!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, MainSeries!, slow_ma!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
        this.PaintSmoothCurve(args, SignalSeries!, slow_ma!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
        this.DrawText(args, Description);
    }
}

