using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DmxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dmx? _dmx;
    private readonly LineSeries? _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DMX {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/dmx/Dmx.Quantower.cs";

    public DmxIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DMX - Jurik Directional Movement Index";
        Description = "Jurik's smoother, lower-lag alternative to DMI/ADX";
        _series = new LineSeries(name: $"DMX {Period}", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dmx = new Dmx(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _dmx!.Update(this.GetInputBar(args), args.IsNewBar());

        _series!.SetValue(result.Value);
        _series!.SetMarker(0, Color.Transparent);
    }
}
