using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// TtmLrc: TTM Linear Regression Channel - Quantower Indicator Adapter
/// John Carter's Linear Regression Channel with ±1σ and ±2σ standard deviation bands.
/// Middle = Linear regression line value at current bar
/// Upper1/Lower1 = ±1 standard deviation (68% price range)
/// Upper2/Lower2 = ±2 standard deviations (95% price range)
/// </summary>
public sealed class TtmLrcIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, minimum: 2, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 100;

    [InputParameter("Price Type", sortIndex: 20)]
    public PriceType SourceType { get; set; } = PriceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private TtmLrc? _indicator;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"TtmLrc({Period})";

    public TtmLrcIndicator()
    {
        Name = "TTM LRC - Linear Regression Channel";
        Description = "John Carter's Linear Regression Channel with ±1σ and ±2σ bands";
        SeparateWindow = false;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _indicator = new TtmLrc(Period);

        // Middle line (regression line)
        AddLineSeries(new LineSeries("Midline", Color.DodgerBlue, 2, LineStyle.Solid));

        // ±1 StdDev bands (inner bands)
        AddLineSeries(new LineSeries("Upper1", Color.FromArgb(100, 255, 100), 1, LineStyle.Solid));
        AddLineSeries(new LineSeries("Lower1", Color.FromArgb(255, 100, 100), 1, LineStyle.Solid));

        // ±2 StdDev bands (outer bands)
        AddLineSeries(new LineSeries("Upper2", Color.FromArgb(50, 200, 50), 1, LineStyle.Dash));
        AddLineSeries(new LineSeries("Lower2", Color.FromArgb(200, 50, 50), 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_indicator is null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        bool isNew = args.IsNewBar();

        TValue input = new(
            time: item.TimeLeft,
            value: item[SourceType]
        );

        _indicator.Update(input, isNew);

        bool isHot = _indicator.IsHot;

        LinesSeries[0].SetValue(_indicator.Midline.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(_indicator.Upper1.Value, isHot, ShowColdValues);
        LinesSeries[2].SetValue(_indicator.Lower1.Value, isHot, ShowColdValues);
        LinesSeries[3].SetValue(_indicator.Upper2.Value, isHot, ShowColdValues);
        LinesSeries[4].SetValue(_indicator.Lower2.Value, isHot, ShowColdValues);
    }
}
