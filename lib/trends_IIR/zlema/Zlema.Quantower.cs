using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class ZlemaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Zlema ma = null!;
    protected LineSeries Series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ZLEMA {Period}:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_IIR/zlema/Zlema.Quantower.cs";

    public ZlemaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "ZLEMA - Zero-Lag Exponential Moving Average";
        Description = "Zero-lag EMA using lagged price compensation.";
        Series = new LineSeries(name: $"ZLEMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Zlema(Period);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        Series.SetValue(result.Value, ma.IsHot, ShowColdValues);
    }
}
