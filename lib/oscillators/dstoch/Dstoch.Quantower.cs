using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DstochIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 500, 1, 0)]
    public int Period { get; set; } = 21;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dstoch _dstoch = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DSTOCH {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/dstoch/Dstoch.cs";

    public DstochIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DSTOCH";
        Description = "Double Stochastic (Bressert DSS) — Stochastic applied to Stochastic with EMA smoothing";

        _series = new LineSeries(name: "DSS", color: Color.Blue, width: 2, style: LineStyle.Solid);

        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dstoch = new Dstoch(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _dstoch.Update(this.GetInputBar(args), args.IsNewBar());

        _series.SetValue(_dstoch.Last.Value, _dstoch.IsHot, ShowColdValues);
    }
}
