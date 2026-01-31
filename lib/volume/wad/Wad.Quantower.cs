using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class WadIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Wad _wad = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "WAD";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/wad/Wad.Quantower.cs";

    public WadIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "WAD - Williams Accumulation/Distribution";
        Description = "Williams Accumulation/Distribution";

        _series = new LineSeries(name: "WAD", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _wad = new Wad();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _wad.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _wad.IsHot, ShowColdValues);
    }
}