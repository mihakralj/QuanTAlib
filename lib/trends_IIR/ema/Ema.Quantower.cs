using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class EmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ema ma = null!;
    protected LineSeries Series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"EMA {Period}:{SourceName}";

    public EmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "EMA - Exponential Moving Average";
        Description = "Exponential Moving Average";
        Series = new LineSeries(name: $"EMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Ema(Period);
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
