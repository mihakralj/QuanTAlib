using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MidpriceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Midprice _midprice = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MIDPRICE({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/core/midprice/Midprice.Quantower.cs";

    public MidpriceIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "MIDPRICE - Midpoint Price";
        Description = "Midpoint of rolling highest high and lowest low over a period: (HH+LL)/2.";

        _series = new LineSeries(name: "MIDPRICE", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _midprice = new Midprice(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _midprice.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _midprice.IsHot, ShowColdValues);
    }
}
