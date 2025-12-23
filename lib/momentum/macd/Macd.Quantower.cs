using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class MacdIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Signal Period", sortIndex: 3, 1, 2000, 1, 0)]
    public int SignalPeriod { get; set; } = 9;

    private Macd? _macd;
    protected LineSeries? MacdSeries;
    protected LineSeries? SignalSeries;
    protected LineSeries? HistSeries;

    public int MinHistoryDepths => Math.Max(FastPeriod, SlowPeriod) + SignalPeriod;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MACD({FastPeriod},{SlowPeriod},{SignalPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/macd/Macd.Quantower.cs";

    public MacdIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "MACD - Moving Average Convergence Divergence";
        Description = "Trend-following momentum indicator";

        MacdSeries = new(name: "MACD", color: Color.Blue, width: 2, style: LineStyle.Solid);
        SignalSeries = new(name: "Signal", color: Color.Red, width: 2, style: LineStyle.Solid);
        HistSeries = new(name: "Histogram", color: Color.Green, width: 2, style: LineStyle.Solid); // Quantower LineStyle doesn't have Histogram, use Solid and we'll paint it manually if needed, or just use Solid for now. Actually, Quantower usually handles Histogram via a different series type or style, but LineSeries only supports lines. Let's stick to Solid for now to fix compilation.

        AddLineSeries(MacdSeries);
        AddLineSeries(SignalSeries);
        AddLineSeries(HistSeries);
    }

    protected override void OnInit()
    {
        _macd = new Macd(FastPeriod, SlowPeriod, SignalPeriod);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TValue input = this.GetInputValue(args, SourceType.Close);

        _macd!.Update(input, isNew);

        MacdSeries!.SetValue(_macd.Last.Value);
        SignalSeries!.SetValue(_macd.Signal.Value);
        HistSeries!.SetValue(_macd.Histogram.Value);
    }
}
