using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// ROC (Rate of Change) Quantower indicator.
/// Calculates absolute price change over a lookback period.
/// Formula: current - past
/// </summary>
public class RocIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period { get; set; } = 9;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Roc? _roc;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period + 1;
    public override string ShortName => $"ROC({Period})";

    public RocIndicator()
    {
        Name = "ROC - Rate of Change (Absolute)";
        Description = "Calculates absolute price change: current - past";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _roc = new Roc(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("ROC", IndicatorExtensions.Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_roc == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _roc.Update(input, isNew);

        bool isHot = _roc.IsHot;

        LinesSeries[0].SetValue(_roc.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double roc = _roc.Last.Value;
            Color color;
            if (roc > 0)
            {
                color = Color.Green;
            }
            else if (roc < 0)
            {
                color = Color.Red;
            }
            else
            {
                color = Color.Gray;
            }

            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(color));
        }
    }
}
