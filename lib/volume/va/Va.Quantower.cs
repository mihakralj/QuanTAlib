using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class VaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Va _va = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 1;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 1;

    public override string ShortName => "VA";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/va/Va.Quantower.cs";

    public VaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "VA - Volume Accumulation";
        Description = "Cumulative volume indicator that measures volume flow relative to the midpoint of each bar's range.";

        _series = new LineSeries(name: "VA", color: Color.Cyan, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _va = new Va();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _va.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _va.IsHot, ShowColdValues);
    }
}