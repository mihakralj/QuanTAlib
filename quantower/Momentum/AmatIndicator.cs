using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// AMAT (Archer Moving Averages Trends) Quantower indicator.
/// Uses dual EMAs to identify trend direction and strength.
/// </summary>
public class AmatIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 10, minimum: 1, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int FastPeriod { get; set; } = 10;

    [InputParameter("Slow Period", sortIndex: 11, minimum: 2, maximum: 1000, increment: 1, decimalPlaces: 0)]
    public int SlowPeriod { get; set; } = 50;

    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Amat? _amat;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => SlowPeriod;
    public override string ShortName => $"AMAT({FastPeriod},{SlowPeriod})";

    public AmatIndicator()
    {
        Name = "AMAT - Archer Moving Averages Trends";
        Description = "Identifies trend direction using dual EMA alignment";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _amat = new Amat(FastPeriod, SlowPeriod);
        _selector = Source.GetPriceSelector();

        // Trend line: +1 = bullish, -1 = bearish, 0 = neutral
        AddLineSeries(new LineSeries("Trend", Momentum, 2, LineStyle.Histogramm));

        // Strength line: percentage separation
        AddLineSeries(new LineSeries("Strength", Color.FromArgb(255, 200, 128), 1, LineStyle.Solid));

        // Fast EMA line
        AddLineSeries(new LineSeries("Fast EMA", Color.FromArgb(100, 200, 100), 1, LineStyle.Solid));

        // Slow EMA line
        AddLineSeries(new LineSeries("Slow EMA", Color.FromArgb(200, 100, 100), 1, LineStyle.Solid));

        // Zero line reference
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_amat == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _amat.Update(input, isNew);

        bool isHot = _amat.IsHot;

        // Trend line
        LinesSeries[0].SetValue(_amat.Last.Value, isHot, ShowColdValues);

        // Strength line
        LinesSeries[1].SetValue(_amat.Strength.Value, isHot, ShowColdValues);

        // Fast EMA line
        LinesSeries[2].SetValue(_amat.FastEma.Value, isHot, ShowColdValues);

        // Slow EMA line
        LinesSeries[3].SetValue(_amat.SlowEma.Value, isHot, ShowColdValues);

        // Zero reference line
        LinesSeries[4].SetValue(0);

        // Color the trend histogram based on direction
        if (isHot || ShowColdValues)
        {
            double trend = _amat.Last.Value;
            Color trendColor = trend > 0 ? Color.Green :
                               trend < 0 ? Color.Red :
                               Color.Gray;
            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(trendColor));
        }
    }
}
