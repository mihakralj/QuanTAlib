using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// TDIST (Student's t-Distribution CDF) Quantower indicator.
/// Computes the one-tailed t-CDF applied to a min-max normalized price series
/// scaled to t ∈ [-3, +3] over a rolling lookback window.
/// </summary>
public class TdistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Degrees of Freedom (ν)", sortIndex: 0, minimum: 1, maximum: 999, increment: 1)]
    public int Nu { get; set; } = 10;

    [InputParameter("Period", sortIndex: 1, minimum: 2, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Tdist? _tdist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"TDIST({Nu},{Period})";

    public TdistIndicator()
    {
        Name = "TDIST - Student's t-Distribution CDF";
        Description = "Applies the Student's t-Distribution CDF to a min-max normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _tdist = new Tdist(Nu, Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("TDist", Color.Cyan, 2, LineStyle.Solid));
        // Reference level at 0.5 (symmetric midpoint of t-distribution)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_tdist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _tdist.Update(input, isNew);

        bool isHot = _tdist.IsHot;

        LinesSeries[0].SetValue(_tdist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
