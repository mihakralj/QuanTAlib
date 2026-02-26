using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// BINOMDIST (Binomial Distribution CDF) Quantower indicator.
/// Computes P(X ≤ k) for X ~ Binomial(n, p), where p is derived from the
/// min-max normalized price within a rolling lookback window.
/// </summary>
public class BinomdistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Period", sortIndex: 0, minimum: 1, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 50;

    [InputParameter("Trials (n)", sortIndex: 1, minimum: 1, maximum: 1000, increment: 1)]
    public int Trials { get; set; } = 20;

    [InputParameter("Threshold (k)", sortIndex: 2, minimum: 0, maximum: 1000, increment: 1)]
    public int Threshold { get; set; } = 10;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Binomdist? _binomdist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"BINOMDIST({Period},{Trials},{Threshold})";

    public BinomdistIndicator()
    {
        Name = "BINOMDIST - Binomial Distribution CDF";
        Description = "Computes P(X ≤ k) for X ~ Binomial(n, p) from min-max normalized price";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _binomdist = new Binomdist(Period, Trials, Threshold);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("BinomDist", Color.Yellow, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_binomdist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _binomdist.Update(input, isNew);

        bool isHot = _binomdist.IsHot;

        LinesSeries[0].SetValue(_binomdist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
