using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CmfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cmf _cmf = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 20;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CMF({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/cmf/Cmf.Quantower.cs";

    public CmfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CMF - Chaikin Money Flow";
        Description = "Chaikin Money Flow measures buying and selling pressure over a specified period";

        _series = new LineSeries(name: "CMF", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _cmf = new Cmf(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _cmf.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _cmf.IsHot, ShowColdValues);
    }
}
