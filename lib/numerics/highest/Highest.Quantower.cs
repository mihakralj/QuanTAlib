using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// HIGHEST (Rolling Maximum) Quantower indicator.
/// Calculates the maximum value over a rolling lookback window.
/// </summary>
public class HighestIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 1, maximum: 1000)]
    public int Period { get; set; } = 14;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.High;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Highest? _highest;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"HIGHEST({Period})";

    public HighestIndicator()
    {
        Name = "HIGHEST - Rolling Maximum";
        Description = "Calculates the maximum value over a rolling lookback window";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _highest = new Highest(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Highest", Color.Green, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_highest == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _highest.Update(input, isNew);

        bool isHot = _highest.IsHot;

        LinesSeries[0].SetValue(_highest.Last.Value, isHot, ShowColdValues);
    }
}
