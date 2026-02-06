using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ChopIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Chop _chop = null!;
    private readonly LineSeries _chopSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CHOP {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/chop/Chop.Quantower.cs";

    public ChopIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "Choppiness Index";
        Description = "Measures market trendiness (E.W. Dreiss)";

        _chopSeries = new LineSeries(name: "CHOP", color: Color.Yellow, width: 2, style: LineStyle.Solid);

        AddLineSeries(_chopSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _chop = new Chop(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _chop.Update(this.GetInputBar(args), args.IsNewBar());

        _chopSeries.SetValue(result.Value, _chop.IsHot, ShowColdValues);
    }
}
