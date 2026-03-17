using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DmhIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dmh _dmh = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DMH {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/dmh/Dmh.Quantower.cs";

    public DmhIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DMH - Ehlers Directional Movement with Hann";
        Description = "Ehlers' improved directional movement with Hann windowed FIR smoothing";
        _series = new LineSeries(name: $"DMH {Period}", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dmh = new Dmh(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _dmh.Update(this.GetInputBar(args), args.IsNewBar());

        _series.SetValue(result.Value);
        _series.SetMarker(0, Color.Transparent);
    }
}
