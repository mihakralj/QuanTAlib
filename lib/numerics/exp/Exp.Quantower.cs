using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// EXP (Exponential Function) Quantower indicator.
/// Transforms values using the natural exponential function e^x.
/// </summary>
public class ExpIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Exp? _exp;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => "EXP";

    public ExpIndicator()
    {
        Name = "EXP - Exponential Function";
        Description = "Transforms values using the natural exponential function e^x";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _exp = new Exp();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Exp", Color.Green, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_exp == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _exp.Update(input, isNew);

        bool isHot = _exp.IsHot;

        LinesSeries[0].SetValue(_exp.Last.Value, isHot, ShowColdValues);
    }
}
