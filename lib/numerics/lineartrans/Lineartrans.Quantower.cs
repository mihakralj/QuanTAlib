using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// LINEARTRANS (Linear Scaling) Quantower indicator.
/// Transforms values using y = slope * x + intercept.
/// </summary>
public class LineartransIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Slope", sortIndex: 10, minimum: -1e10, maximum: 1e10, decimalPlaces: 4)]
    public double Slope { get; set; } = 1.0;

    [InputParameter("Intercept", sortIndex: 20, minimum: -1e10, maximum: 1e10, decimalPlaces: 4)]
    public double Intercept { get; set; } = 0.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Lineartrans? _lineartrans;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => $"LINEARTRANS({Slope},{Intercept})";

    public LineartransIndicator()
    {
        Name = "LINEARTRANS - Linear Scaling";
        Description = "Transforms values using y = slope * x + intercept";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _lineartrans = new Lineartrans(Slope, Intercept);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Lineartrans", Color.Cyan, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_lineartrans == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _lineartrans.Update(input, isNew);

        bool isHot = _lineartrans.IsHot;

        LinesSeries[0].SetValue(_lineartrans.Last.Value, isHot, ShowColdValues);
    }
}