using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// POISSONDIST (Poisson Distribution CDF) Quantower indicator.
/// Computes P(X ≤ k; λ) where λ is derived from the min-max normalized price
/// within a rolling lookback window.
/// </summary>
public class PoissondistIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Period", sortIndex: 0, minimum: 2, maximum: 2000, increment: 1)]
    public int Period { get; set; } = 14;

    [InputParameter("Lambda Scale (λ)", sortIndex: 1, minimum: 0.01, maximum: 100.0, increment: 0.5, decimalPlaces: 2)]
    public double Lambda { get; set; } = 1.0;

    [InputParameter("Threshold (k)", sortIndex: 2, minimum: 0, maximum: 200, increment: 1)]
    public int Threshold { get; set; } = 5;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Poissondist? _poissondist;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"POISSONDIST({Period},{Lambda:F2},{Threshold})";

    public PoissondistIndicator()
    {
        Name = "POISSONDIST - Poisson Distribution CDF";
        Description = "Computes P(X ≤ k; λ) for Poisson CDF from min-max normalized price";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _poissondist = new Poissondist(Lambda, Period, Threshold);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("PoissonDist", Color.Yellow, 2, LineStyle.Solid));
        // Reference level at 0.5 (midpoint)
        AddLineSeries(new LineSeries("Mid", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_poissondist == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _poissondist.Update(input, isNew);

        bool isHot = _poissondist.IsHot;

        LinesSeries[0].SetValue(_poissondist.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.5, isHot, ShowColdValues);
    }
}
