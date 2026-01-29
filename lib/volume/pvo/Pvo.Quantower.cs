using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PvoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 10, 1, 500, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 11, 1, 500, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Signal Period", sortIndex: 12, 1, 500, 1, 0)]
    public int SignalPeriod { get; set; } = 9;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pvo _pvo = null!;
    private readonly LineSeries _pvoSeries;
    private readonly LineSeries _signalSeries;
    private readonly LineSeries _histogramSeries;

    public int MinHistoryDepths => SlowPeriod;
    int IWatchlistIndicator.MinHistoryDepths => SlowPeriod;

    public override string ShortName => $"PVO({FastPeriod},{SlowPeriod},{SignalPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/pvo/Pvo.Quantower.cs";

    public PvoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PVO - Percentage Volume Oscillator";
        Description = "Percentage Volume Oscillator measures the difference between two volume EMAs as a percentage of the slower EMA";

        _pvoSeries = new LineSeries(name: "PVO", color: Color.Cyan, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Red, width: 1, style: LineStyle.Solid);
        _histogramSeries = new LineSeries(name: "Histogram", color: Color.Gray, width: 1, style: LineStyle.Histogramm);
        AddLineSeries(_pvoSeries);
        AddLineSeries(_signalSeries);
        AddLineSeries(_histogramSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _pvo = new Pvo(FastPeriod, SlowPeriod, SignalPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _pvo.Update(bar, args.IsNewBar());

        _pvoSeries.SetValue(result.Value, _pvo.IsHot, ShowColdValues);
        _signalSeries.SetValue(_pvo.Signal.Value, _pvo.IsHot, ShowColdValues);
        _histogramSeries.SetValue(_pvo.Histogram.Value, _pvo.IsHot, ShowColdValues);
    }
}