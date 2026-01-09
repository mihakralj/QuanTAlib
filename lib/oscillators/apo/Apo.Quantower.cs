using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ApoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, 1, 1000, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Apo? _apo;
    private readonly LineSeries? _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"APO {FastPeriod}:{SlowPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/apo/Apo.Quantower.cs";

    public ApoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "APO - Absolute Price Oscillator";
        Description = "Momentum indicator showing the difference between two EMAs";

        _series = new(name: "APO", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _apo = new Apo(FastPeriod, SlowPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _apo!.Update(this.GetInputBar(args), args.IsNewBar());

        _series!.SetValue(result.Value, _apo.IsHot, ShowColdValues);
    }
}
