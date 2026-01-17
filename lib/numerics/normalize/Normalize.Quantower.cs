using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// NORMALIZE (Min-Max Normalization) Quantower indicator.
/// Scales values to [0, 1] range using min-max scaling over a lookback period.
/// </summary>
public class NormalizeIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Period", sortIndex: 0, minimum: 1, maximum: 1000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Normalize? _normalize;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"NORM({Period})";

    public NormalizeIndicator()
    {
        Name = "NORMALIZE - Min-Max Normalization";
        Description = "Scales values to [0, 1] range using min-max scaling over a lookback period";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _normalize = new Normalize(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Normalize", Color.Green, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_normalize == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _normalize.Update(input, isNew);

        bool isHot = _normalize.IsHot;

        LinesSeries[0].SetValue(_normalize.Last.Value, isHot, ShowColdValues);
    }
}
