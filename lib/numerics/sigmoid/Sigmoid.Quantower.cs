using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// SIGMOID (Logistic Function) Quantower indicator.
/// Maps any real-valued input to the range (0, 1) using the logistic function.
/// </summary>
public class SigmoidIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Steepness (k)", sortIndex: 10, minimum: 0.01, maximum: 100, increment: 0.1, decimalPlaces: 2)]
    public double Steepness { get; set; } = 1.0;

    [InputParameter("Midpoint (x0)", sortIndex: 20, minimum: -10000, maximum: 10000, increment: 1, decimalPlaces: 2)]
    public double Midpoint { get; set; } = 0.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Sigmoid? _sigmoid;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => $"SIGMOID({Steepness:F2},{Midpoint:F2})";

    public SigmoidIndicator()
    {
        Name = "SIGMOID - Logistic Function";
        Description = "Maps any real-valued input to the range (0, 1) using the logistic function";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _sigmoid = new Sigmoid(Steepness, Midpoint);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Sigmoid", Color.Orange, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_sigmoid == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _sigmoid.Update(input, isNew);

        bool isHot = _sigmoid.IsHot;

        LinesSeries[0].SetValue(_sigmoid.Last.Value, isHot, ShowColdValues);
    }
}
