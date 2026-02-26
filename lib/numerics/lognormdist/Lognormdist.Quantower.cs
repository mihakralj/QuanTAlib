using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// LOGNORMDIST (Log-Normal Distribution CDF) Quantower indicator.
/// Computes F(x; μ, σ) = Φ((ln(x) - μ) / σ) applied to a min-max normalized
/// price series over a rolling lookback window.
/// </summary>
public class LognormdistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Log-Mean (μ)", sortIndex: 0, minimum: -100.0, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double Mu { get; set; } = 0.0;

    [InputParameter("Log-Std (σ)", sortIndex: 1, minimum: 0.001, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double Sigma { get; set; } = 1.0;

    [InputParameter("Period", sortIndex: 2, minimum: 2, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Lognormdist? _lognormdist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"LOGNORMDIST({Mu:F2},{Sigma:F2},{Period})";

    public LognormdistIndicator()
    {
        Name = "LOGNORMDIST - Log-Normal Distribution CDF";
        Description = "Applies the log-normal CDF to a min-max normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _lognormdist = new Lognormdist(Mu, Sigma, Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("LogNormDist", Color.Yellow, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_lognormdist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _lognormdist.Update(input, isNew);

        bool isHot = _lognormdist.IsHot;

        LinesSeries[0].SetValue(_lognormdist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
