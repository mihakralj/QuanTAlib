using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// FDIST (F-Distribution CDF) Quantower indicator.
/// Computes F(x; d1, d2) = I(d1·x/(d1·x+d2), d1/2, d2/2) applied to a
/// min-max normalized price series over a rolling lookback window.
/// </summary>
public class FdistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Numerator DoF (d1)", sortIndex: 0, minimum: 1, maximum: 999, increment: 1)]
    public int D1 { get; set; } = 1;

    [InputParameter("Denominator DoF (d2)", sortIndex: 1, minimum: 1, maximum: 999, increment: 1)]
    public int D2 { get; set; } = 1;

    [InputParameter("Period", sortIndex: 2, minimum: 2, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Fdist? _fdist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"FDIST({D1},{D2},{Period})";

    public FdistIndicator()
    {
        Name = "FDIST - F-Distribution CDF";
        Description = "Applies the F-Distribution (Fisher-Snedecor) CDF to a min-max normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _fdist = new Fdist(D1, D2, Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("FDist", Color.Cyan, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_fdist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _fdist.Update(input, isNew);

        bool isHot = _fdist.IsHot;

        LinesSeries[0].SetValue(_fdist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
