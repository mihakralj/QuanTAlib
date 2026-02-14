using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CkstopIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("ATR Period", sortIndex: 0, 1, 500, 1, 0)]
    public int AtrPeriod { get; set; } = 10;

    [InputParameter("Multiplier", sortIndex: 1, 0.1, 10.0, 0.1, 1)]
    public double Multiplier { get; set; } = 1.0;

    [InputParameter("Stop Period", sortIndex: 2, 1, 500, 1, 0)]
    public int StopPeriod { get; set; } = 9;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ckstop _indicator = null!;
    private readonly LineSeries _stopLongSeries;
    private readonly LineSeries _stopShortSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CKSTOP({AtrPeriod},{Multiplier:F1},{StopPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/ckstop/Ckstop.cs";

    public CkstopIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "CKSTOP - Chande Kroll Stop";
        Description = "ATR-based trailing stop indicator. Two overlay lines: StopLong (green) for long position stops, StopShort (red) for short position stops.";

        _stopLongSeries = new LineSeries(name: "Stop Long", color: Color.Green, width: 2, style: LineStyle.Solid);
        _stopShortSeries = new LineSeries(name: "Stop Short", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_stopLongSeries);
        AddLineSeries(_stopShortSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Ckstop(AtrPeriod, Multiplier, StopPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _stopLongSeries.SetValue(_indicator.StopLong, _indicator.IsHot, ShowColdValues);
        _stopShortSeries.SetValue(_indicator.StopShort, _indicator.IsHot, ShowColdValues);
    }
}
