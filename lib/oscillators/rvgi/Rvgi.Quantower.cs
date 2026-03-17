using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RvgiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 5000, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rvgi _rvgi = null!;
    private readonly LineSeries _rvgiLine;
    private readonly LineSeries _signalLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RVGI ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/rvgi/Rvgi.Quantower.cs";

    public RvgiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "RVGI - Ehlers Relative Vigor Index";
        Description = "Dual-output oscillator comparing closing strength to the full bar range, smoothed via 4-tap SWMA and averaged over a period. RVGI > 0 in uptrends, < 0 in downtrends.";

        _rvgiLine = new LineSeries("RVGI", Color.Yellow, 2, LineStyle.Solid);
        _signalLine = new LineSeries("Signal", Color.Cyan, 1, LineStyle.Solid);

        AddLineSeries(_rvgiLine);
        AddLineSeries(_signalLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _rvgi = new Rvgi(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _rvgi.Update(this.GetInputBar(args), args.IsNewBar());

        _rvgiLine.SetValue(_rvgi.RvgiValue, _rvgi.IsHot, ShowColdValues);
        _signalLine.SetValue(_rvgi.Signal, _rvgi.IsHot, ShowColdValues);
    }
}
