using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// MIDPOINT (Rolling Range Midpoint) Quantower indicator.
/// Calculates (Highest + Lowest) / 2 over a rolling lookback window.
/// </summary>
public class MidpointIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 0, minimum: 1, maximum: 1000)]
    public int Period { get; set; } = 14;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Midpoint? _midpoint;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"MIDPOINT({Period})";

    public MidpointIndicator()
    {
        Name = "MIDPOINT - Rolling Range Midpoint";
        Description = "Calculates (Highest + Lowest) / 2 over a rolling lookback window";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _midpoint = new Midpoint(Period);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Midpoint", Color.Blue, 2, LineStyle.Solid));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_midpoint == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _midpoint.Update(input, isNew);

        bool isHot = _midpoint.IsHot;

        LinesSeries[0].SetValue(_midpoint.Last.Value, isHot, ShowColdValues);
    }
}
