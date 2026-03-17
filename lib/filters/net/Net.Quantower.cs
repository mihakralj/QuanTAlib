using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class NetIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 100, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Net _net = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"NET({Period}):{Source}";

    public NetIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "NET - Ehlers Noise Elimination Technology";
        Description = "Kendall Tau-a rank correlation for noise elimination";
        _series = new LineSeries(name: $"NET {Period}", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _net = new Net(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _net.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _series.SetValue(value, _net.IsHot, ShowColdValues);
    }
}
