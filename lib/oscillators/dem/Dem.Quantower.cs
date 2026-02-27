using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DemIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 5000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dem _dem = null!;
    private readonly LineSeries _demLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DEM ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/dem/Dem.Quantower.cs";

    public DemIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DEM - DeMarker Oscillator";
        Description = "Bounded [0,1] oscillator comparing sequential highs and lows. Values near 0.3 indicate oversold; near 0.7 indicate overbought.";

        _demLine = new LineSeries("DEM", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_demLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dem = new Dem(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _dem.Update(this.GetInputBar(args), args.IsNewBar());

        _demLine.SetValue(_dem.Last.Value, _dem.IsHot, ShowColdValues);
    }
}
