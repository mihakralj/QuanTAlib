using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// SQRT (Square Root Transform) Quantower indicator.
/// Transforms values using the square root function √x for variance stabilization.
/// </summary>
public class SqrtIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Sqrt? _sqrt;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => "SQRT";

    public SqrtIndicator()
    {
        Name = "SQRT - Square Root Transform";
        Description = "Transforms values using the square root function √x for variance stabilization";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _sqrt = new Sqrt();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Sqrt", Color.Blue, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_sqrt == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _sqrt.Update(input, isNew);

        bool isHot = _sqrt.IsHot;

        LinesSeries[0].SetValue(_sqrt.Last.Value, isHot, ShowColdValues);
    }
}