using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Limit Move (T)", sortIndex: 1, 0.001, 10000.0, 0.001, 3)]
    public double LimitMove { get; set; } = 3.0;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Asi _asi = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ASI({LimitMove})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/asi/Asi.Quantower.cs";

    public AsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ASI - Accumulation Swing Index";
        Description = "Wilder's cumulative swing index measuring genuine directional price strength";
        _series = new LineSeries(name: "ASI", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _asi = new Asi(LimitMove);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _asi.Update(this.GetInputBar(args), args.IsNewBar());

        _series.SetValue(result.Value, _asi.IsHot, ShowColdValues);
        _series.SetMarker(0, Color.Transparent);
    }
}
