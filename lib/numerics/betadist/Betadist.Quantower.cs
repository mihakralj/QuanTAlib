using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// BETADIST (Beta Distribution CDF) Quantower indicator.
/// Computes the regularized incomplete beta function I_x(alpha, beta) applied to
/// a min-max normalized price series over a rolling lookback window.
/// </summary>
public class BetadistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Period", sortIndex: 0, minimum: 1, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 50;

    [InputParameter("Alpha", sortIndex: 1, minimum: 0.01, maximum: 100.0, increment: 0.1, decimalPlaces: 2)]
    public double Alpha { get; set; } = 2.0;

    [InputParameter("Beta", sortIndex: 2, minimum: 0.01, maximum: 100.0, increment: 0.1, decimalPlaces: 2)]
    public double BetaParam { get; set; } = 2.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Betadist? _betadist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"BETADIST({Period},{Alpha:F1},{BetaParam:F1})";

    public BetadistIndicator()
    {
        Name = "BETADIST - Beta Distribution CDF";
        Description = "Applies the regularized incomplete beta function to a min-max normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _betadist = new Betadist(Period, Alpha, BetaParam);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("BetaDist", Color.Cyan, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_betadist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _betadist.Update(input, isNew);

        bool isHot = _betadist.IsHot;

        LinesSeries[0].SetValue(_betadist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
