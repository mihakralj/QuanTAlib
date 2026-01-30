using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Short Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1)]
    public int ShortPeriod { get; set; } = 5;

    [InputParameter("Long Period", sortIndex: 11, minimum: 2, maximum: 1000, increment: 1)]
    public int LongPeriod { get; set; } = 10;

    [InputParameter("Signal Period", sortIndex: 12, minimum: 1, maximum: 500, increment: 1)]
    public int SignalPeriod { get; set; } = 10;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Vo _vo = null!;
    private readonly LineSeries _voSeries;
    private readonly LineSeries _signalSeries;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => LongPeriod;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => LongPeriod;

    public override string ShortName => $"VO({ShortPeriod},{LongPeriod},{SignalPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/vo/Vo.Quantower.cs";

    public VoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VO - Volume Oscillator";
        Description = "Measures the difference between two volume moving averages as a percentage.";

        _voSeries = new LineSeries(name: "VO", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_voSeries);
        AddLineSeries(_signalSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _vo = new Vo(ShortPeriod, LongPeriod, SignalPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _vo.Update(bar, args.IsNewBar());

        _voSeries.SetValue(result.Value, _vo.IsHot, ShowColdValues);
        _signalSeries.SetValue(_vo.Signal, _vo.IsHot, ShowColdValues);
    }
}