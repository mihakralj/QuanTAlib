using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VwmacdIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Signal Period", sortIndex: 3, 1, 2000, 1, 0)]
    public int SignalPeriod { get; set; } = 9;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vwmacd _vwmacd = null!;
    private readonly LineSeries _vwmacdSeries;
    private readonly LineSeries _signalSeries;
    private readonly LineSeries _histSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"VWMACD({FastPeriod},{SlowPeriod},{SignalPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/vwmacd/Vwmacd.Quantower.cs";

    public VwmacdIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VWMACD - Volume-Weighted MACD";
        Description = "MACD using Volume-Weighted Moving Averages instead of EMAs";

        _vwmacdSeries = new LineSeries(name: "VWMACD", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Red, width: 2, style: LineStyle.Solid);
        _histSeries = new LineSeries(name: "Histogram", color: Color.Green, width: 2, style: LineStyle.Solid);

        AddLineSeries(_vwmacdSeries);
        AddLineSeries(_signalSeries);
        AddLineSeries(_histSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vwmacd = new Vwmacd(FastPeriod, SlowPeriod, SignalPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        _vwmacd.Update(bar, args.IsNewBar());

        _vwmacdSeries.SetValue(_vwmacd.Last.Value, _vwmacd.IsHot, ShowColdValues);
        _signalSeries.SetValue(_vwmacd.Signal.Value, _vwmacd.IsHot, ShowColdValues);
        _histSeries.SetValue(_vwmacd.Histogram.Value, _vwmacd.IsHot, ShowColdValues);
    }
}
