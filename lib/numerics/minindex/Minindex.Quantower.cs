using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// MININDEX (Rolling Minimum Index) Quantower indicator.
/// Returns the bars-ago position of the minimum value within a rolling window.
/// </summary>
[SkipLocalsInit]
public class MinindexIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Minindex _indicator = null!;
    protected LineSeries Series;
    protected string SourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"MININDEX({Period})";

    public MinindexIndicator()
    {
        OnBackGround = false;
        SeparateWindow = true;
        SourceName = Source.ToString();
        Name = "MININDEX - Rolling Minimum Index";
        Description = "Returns the bars-ago position of the minimum value within a rolling lookback window.";
        Series = new LineSeries(name: "MININDEX", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Histogramm);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _indicator = new Minindex(Period);
        SourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _indicator.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        Series.SetValue(result.Value, _indicator.IsHot, ShowColdValues);
    }
}
