using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// LINEAR (Linear Scaling) Quantower indicator.
/// Transforms values using y = slope * x + intercept.
/// </summary>
public class LinearIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Slope", sortIndex: 10, minimum: -1e10, maximum: 1e10, decimalPlaces: 4)]
    public double Slope { get; set; } = 1.0;

    [InputParameter("Intercept", sortIndex: 20, minimum: -1e10, maximum: 1e10, decimalPlaces: 4)]
    public double Intercept { get; set; } = 0.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Linear? _linear;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => $"LINEAR({Slope},{Intercept})";

    public LinearIndicator()
    {
        Name = "LINEAR - Linear Scaling";
        Description = "Transforms values using y = slope * x + intercept";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _linear = new Linear(Slope, Intercept);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Linear", Color.Cyan, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_linear == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _linear.Update(input, isNew);

        bool isHot = _linear.IsHot;

        LinesSeries[0].SetValue(_linear.Last.Value, isHot, ShowColdValues);
    }
}
