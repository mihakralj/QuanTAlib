using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TwapIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 0, maximum: 10000, increment: 1)]
    public int Period { get; set; } = 0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Twap _twap = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 1;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 1;

    public override string ShortName => "TWAP";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/twap/Twap.Quantower.cs";

    public TwapIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "TWAP - Time Weighted Average Price";
        Description = "Time Weighted Average Price gives equal weight to each price point within a session. Resets at specified period intervals (0 = never reset).";

        _series = new LineSeries(name: "TWAP", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _twap = new Twap(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _twap.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _twap.IsHot, ShowColdValues);
    }
}