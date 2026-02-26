using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// NORMDIST (Normal Distribution CDF) Quantower indicator.
/// Computes Φ(z; μ, σ) applied to a z-score normalized price series
/// over a rolling lookback window.
/// </summary>
public class NormdistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Mean (μ)", sortIndex: 0, minimum: -100.0, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double Mu { get; set; } = 0.0;

    [InputParameter("Std Dev (σ)", sortIndex: 1, minimum: 0.001, maximum: 100.0, increment: 0.1, decimalPlaces: 3)]
    public double Sigma { get; set; } = 1.0;

    [InputParameter("Period", sortIndex: 2, minimum: 2, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Normdist? _normdist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"NORMDIST({Mu:F2},{Sigma:F2},{Period})";

    public NormdistIndicator()
    {
        Name = "NORMDIST - Normal Distribution CDF";
        Description = "Applies the Gaussian CDF to a z-score normalized price series";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _normdist = new Normdist(Mu, Sigma, Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("NormDist", Color.Cyan, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint / rolling mean)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_normdist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _normdist.Update(input, isNew);

        bool isHot = _normdist.IsHot;

        LinesSeries[0].SetValue(_normdist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
