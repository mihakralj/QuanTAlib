using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class WclpriceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Wclprice _wclprice = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "WCLPRICE";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/core/wclprice/Wclprice.Quantower.cs";

    public WclpriceIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "WCLPRICE - Weighted Close Price";
        Description = "Weighted average emphasizing Close: (H+L+2*C)/4.";

        _series = new LineSeries(name: "WCLPRICE", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _wclprice = new Wclprice();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _wclprice.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _wclprice.IsHot, ShowColdValues);
    }
}
