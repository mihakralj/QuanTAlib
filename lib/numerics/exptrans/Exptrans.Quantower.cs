using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// EXPTRANS (Exponential Function) Quantower indicator.
/// Transforms values using the natural exponential function e^x.
/// </summary>
public class ExptransIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Exptrans? _exptrans;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => "Exptrans";

    public ExptransIndicator()
    {
        Name = "EXPTRANS - Exponential Function";
        Description = "Transforms values using the natural exponential function e^x";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _exptrans = new Exptrans();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Exptrans", Color.Green, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_exptrans == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _exptrans.Update(input, isNew);

        bool isHot = _exptrans.IsHot;

        LinesSeries[0].SetValue(_exptrans.Last.Value, isHot, ShowColdValues);
    }
}
