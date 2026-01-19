using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// LOWEST (Rolling Minimum) Quantower indicator.
/// Calculates the minimum value over a rolling lookback window.
/// </summary>
public class LowestIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 1, maximum: 1000)]
    public int Period { get; set; } = 14;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Low;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Lowest? _lowest;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"LOWEST({Period})";

    public LowestIndicator()
    {
        Name = "LOWEST - Rolling Minimum";
        Description = "Calculates the minimum value over a rolling lookback window";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _lowest = new Lowest(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Lowest", Color.Red, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_lowest == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _lowest.Update(input, isNew);

        bool isHot = _lowest.IsHot;

        LinesSeries[0].SetValue(_lowest.Last.Value, isHot, ShowColdValues);
    }
}
