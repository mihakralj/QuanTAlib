using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AvgpriceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Avgprice _avgprice = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "AVGPRICE";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/core/avgprice/Avgprice.Quantower.cs";

    public AvgpriceIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "AVGPRICE - Average Price";
        Description = "Average of Open, High, Low, and Close prices: (O+H+L+C)/4.";

        _series = new LineSeries(name: "AVGPRICE", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _avgprice = new Avgprice();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _avgprice.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _avgprice.IsHot, ShowColdValues);
    }
}
