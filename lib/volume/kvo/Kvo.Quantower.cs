using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class KvoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 10, 1, 500, 1, 0)]
    public int FastPeriod { get; set; } = 34;

    [InputParameter("Slow Period", sortIndex: 11, 1, 500, 1, 0)]
    public int SlowPeriod { get; set; } = 55;

    [InputParameter("Signal Period", sortIndex: 12, 1, 500, 1, 0)]
    public int SignalPeriod { get; set; } = 13;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Kvo _kvo = null!;
    private readonly LineSeries _kvoSeries;
    private readonly LineSeries _signalSeries;

    public int MinHistoryDepths => SlowPeriod;
    int IWatchlistIndicator.MinHistoryDepths => SlowPeriod;

    public override string ShortName => $"KVO({FastPeriod},{SlowPeriod},{SignalPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/kvo/Kvo.Quantower.cs";

    public KvoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "KVO - Klinger Volume Oscillator";
        Description = "Klinger Volume Oscillator measures the long-term trend of money flow while remaining sensitive to short-term fluctuations";

        _kvoSeries = new LineSeries(name: "KVO", color: Color.Cyan, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Red, width: 1, style: LineStyle.Solid);
        AddLineSeries(_kvoSeries);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _kvo = new Kvo(FastPeriod, SlowPeriod, SignalPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _kvo.Update(bar, args.IsNewBar());

        _kvoSeries.SetValue(result.Value, _kvo.IsHot, ShowColdValues);
        _signalSeries.SetValue(_kvo.Signal.Value, _kvo.IsHot, ShowColdValues);
    }
}