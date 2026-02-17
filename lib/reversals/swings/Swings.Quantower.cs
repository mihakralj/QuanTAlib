using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SwingsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Lookback", sortIndex: 10, minimum: 1, maximum: 100, increment: 1)]
    public int Lookback { get; set; } = 5;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Swings _indicator = null!;
    private readonly LineSeries _swingHighSeries;
    private readonly LineSeries _swingLowSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "SWINGS";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/reversals/swings/Swings.cs";

    public SwingsIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "SWINGS - Swing High/Low Detection";
        Description = "Configurable-lookback pattern detector for swing highs (resistance) and swing lows (support).";

        _swingHighSeries = new LineSeries(name: "Swing High", color: Color.Red, width: 2, style: LineStyle.Dot);
        _swingLowSeries = new LineSeries(name: "Swing Low", color: Color.Green, width: 2, style: LineStyle.Dot);

        AddLineSeries(_swingHighSeries);
        AddLineSeries(_swingLowSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _indicator = new Swings(Lookback);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _indicator.Update(this.GetInputBar(args), args.IsNewBar());

        _swingHighSeries.SetValue(_indicator.SwingHigh, _indicator.IsHot, ShowColdValues);
        _swingLowSeries.SetValue(_indicator.SwingLow, _indicator.IsHot, ShowColdValues);
    }
}
