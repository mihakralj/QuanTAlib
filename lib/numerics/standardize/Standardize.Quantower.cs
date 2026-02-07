using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// STANDARDIZE (Z-Score Normalization) Quantower indicator.
/// Calculates the z-score of values over a lookback period using sample standard deviation.
/// </summary>
public class StandardizeIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Period", sortIndex: 0, minimum: 2, maximum: 1000, increment: 1)]
    public int Period { get; set; } = 20;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Standardize? _standardize;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"STND({Period})";

    public StandardizeIndicator()
    {
        Name = "STANDARDIZE - Z-Score Normalization";
        Description = "Calculates the z-score of values over a lookback period using sample standard deviation";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _standardize = new Standardize(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Z-Score", Color.Yellow, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_standardize == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _standardize.Update(input, isNew);

        bool isHot = _standardize.IsHot;

        LinesSeries[0].SetValue(_standardize.Last.Value, isHot, ShowColdValues);
    }
}
