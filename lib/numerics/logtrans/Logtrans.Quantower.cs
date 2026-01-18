using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// LOGTRANS (Natural Logarithm) Quantower indicator.
/// Transforms values using natural logarithm ln(x).
/// </summary>
public class LogtransIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Logtrans? _logtrans;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => "Logtrans";

    public LogtransIndicator()
    {
        Name = "LOGTRANS - Natural Logarithm";
        Description = "Transforms values using natural logarithm ln(x)";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _logtrans = new Logtrans();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Logtrans", Color.Orange, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_logtrans == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _logtrans.Update(input, isNew);

        bool isHot = _logtrans.IsHot;

        LinesSeries[0].SetValue(_logtrans.Last.Value, isHot, ShowColdValues);
    }
}