using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// GAMMADIST (Gamma Distribution CDF) Quantower indicator.
/// Computes F(x; α, β) = P(α, x/β) applied to a min-max normalized price series
/// over a rolling lookback window.
/// </summary>
public class GammadistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Shape (α)", sortIndex: 0, minimum: 0.001, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double Alpha { get; set; } = 2.0;

    [InputParameter("Scale (β)", sortIndex: 1, minimum: 0.001, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double Beta { get; set; } = 1.0;

    [InputParameter("Period", sortIndex: 2, minimum: 2, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Gammadist? _gammadist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"GAMMADIST({Alpha:F2},{Beta:F2},{Period})";

    public GammadistIndicator()
    {
        Name = "GAMMADIST - Gamma Distribution CDF";
        Description = "Applies the Gamma Distribution CDF to a min-max normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _gammadist = new Gammadist(Alpha, Beta, Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("GammaDist", Color.Cyan, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_gammadist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _gammadist.Update(input, isNew);

        bool isHot = _gammadist.IsHot;

        LinesSeries[0].SetValue(_gammadist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
