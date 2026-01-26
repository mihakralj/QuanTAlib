using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// SQRTTRANS (Square Root Transform) Quantower indicator.
/// Transforms values using the square root function √x for variance stabilization.
/// </summary>
public class SqrttransIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Sqrttrans? _sqrttrans;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1;
    public override string ShortName => "Sqrttrans";

    public SqrttransIndicator()
    {
        Name = "SQRTTRANS - Square Root Transform";
        Description = "Transforms values using the square root function √x for variance stabilization";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _sqrttrans = new Sqrttrans();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Sqrttrans", Color.Blue, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_sqrttrans == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _sqrttrans.Update(input, isNew);

        bool isHot = _sqrttrans.IsHot;

        LinesSeries[0].SetValue(_sqrttrans.Last.Value, isHot, ShowColdValues);
    }
}
