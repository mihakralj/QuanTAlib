using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// EXPDIST (Exponential Distribution CDF) Quantower indicator.
/// Computes F(x; λ) = 1 - exp(-λx) applied to a min-max normalized price series
/// over a rolling lookback window.
/// </summary>
public class ExpdistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Period", sortIndex: 0, minimum: 1, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 50;

    [InputParameter("Lambda", sortIndex: 1, minimum: 0.01, maximum: 100.0, increment: 0.1, decimalPlaces: 2)]
    public double Lambda { get; set; } = 3.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Expdist? _expdist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"EXPDIST({Period},{Lambda:F2})";

    public ExpdistIndicator()
    {
        Name = "EXPDIST - Exponential Distribution CDF";
        Description = "Applies the exponential CDF to a min-max normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _expdist = new Expdist(Period, Lambda);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("ExpDist", Color.Cyan, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_expdist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _expdist.Update(input, isNew);

        bool isHot = _expdist.IsHot;

        LinesSeries[0].SetValue(_expdist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
