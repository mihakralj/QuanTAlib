using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TyppriceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Typprice _typprice = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "TYPPRICE";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/core/typprice/Typprice.Quantower.cs";

    public TyppriceIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "TYPPRICE - Typical Price";
        Description = "Average of Open, High, and Low prices: (O+H+L)/3.";

        _series = new LineSeries(name: "TYPPRICE", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _typprice = new Typprice();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _typprice.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _typprice.IsHot, ShowColdValues);
    }
}
