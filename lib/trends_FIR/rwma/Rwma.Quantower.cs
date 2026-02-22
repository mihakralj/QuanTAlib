using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// Quantower adapter for RWMA (Range Weighted Moving Average).
/// </summary>
[SkipLocalsInit]
public sealed class RwmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 10000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rwma _rwma = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RWMA({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_FIR/rwma/Rwma.Quantower.cs";

    public RwmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "RWMA - Range Weighted Moving Average";
        Description = "Range Weighted Moving Average weights each bar's close by its price range (high - low), giving greater influence to volatile bars.";

        _series = new LineSeries(name: "RWMA", color: Color.Cyan, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _rwma = new Rwma(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _rwma.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _rwma.IsHot, ShowColdValues);
    }
}
