using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// CHANGE (Percentage Change) Quantower indicator.
/// Calculates relative price movement over a lookback period.
/// Formula: (current - past) / past
/// </summary>
public class ChangeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period { get; set; } = 1;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Change? _change;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period + 1;
    public override string ShortName => $"CHANGE({Period})";

    public ChangeIndicator()
    {
        Name = "CHANGE - Percentage Change";
        Description = "Calculates relative price movement: (current - past) / past";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _change = new Change(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Change", Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_change == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _change.Update(input, isNew);

        bool isHot = _change.IsHot;

        LinesSeries[0].SetValue(_change.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double change = _change.Last.Value;
            Color color;
            if (change > 0)
                color = Color.Green;
            else if (change < 0)
                color = Color.Red;
            else
                color = Color.Gray;
            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(color));
        }
    }
}
