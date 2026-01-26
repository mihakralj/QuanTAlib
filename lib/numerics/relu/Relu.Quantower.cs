using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// RELU (Rectified Linear Unit) Quantower indicator.
/// Applies max(0, x) transformation to input values.
/// </summary>
public class ReluIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Relu? _relu;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => "RELU";

    public ReluIndicator()
    {
        Name = "RELU - Rectified Linear Unit";
        Description = "Applies max(0, x) transformation to input values";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _relu = new Relu();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("ReLU", Color.Green, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_relu == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _relu.Update(input, isNew);

        bool isHot = _relu.IsHot;

        LinesSeries[0].SetValue(_relu.Last.Value, isHot, ShowColdValues);
    }
}
