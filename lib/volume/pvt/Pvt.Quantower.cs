using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PvtIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pvt _pvt = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 2;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 2;

    public override string ShortName => "PVT";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/pvt/Pvt.Quantower.cs";

    public PvtIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PVT - Price Volume Trend";
        Description = "Price Volume Trend tracks cumulative buying/selling pressure weighted by relative price changes";

        _series = new LineSeries(name: "PVT", color: Color.DarkGreen, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _pvt = new Pvt();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _pvt.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _pvt.IsHot, ShowColdValues);
    }
}