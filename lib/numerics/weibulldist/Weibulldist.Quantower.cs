using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// WEIBULLDIST (Weibull Distribution CDF) Quantower indicator.
/// Computes F(x; k, λ) = 1 - exp(-(x/λ)^k) applied to a min-max normalized
/// price series over a rolling lookback window.
/// </summary>
public class WeibulldistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Shape (k)", sortIndex: 0, minimum: 0.001, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double K { get; set; } = 1.5;

    [InputParameter("Scale (λ)", sortIndex: 1, minimum: 0.001, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double Lambda { get; set; } = 1.0;

    [InputParameter("Period", sortIndex: 2, minimum: 2, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Weibulldist? _weibulldist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"WEIBULLDIST({K:F2},{Lambda:F2},{Period})";

    public WeibulldistIndicator()
    {
        Name = "WEIBULLDIST - Weibull Distribution CDF";
        Description = "Applies the Weibull CDF to a min-max normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _weibulldist = new Weibulldist(K, Lambda, Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("WeibullDist", Color.Yellow, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_weibulldist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _weibulldist.Update(input, isNew);

        bool isHot = _weibulldist.IsHot;

        LinesSeries[0].SetValue(_weibulldist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
