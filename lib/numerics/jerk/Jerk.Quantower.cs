using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// JERK (Third Derivative) Quantower indicator.
/// Measures the rate of change of acceleration - derivative of accel.
/// </summary>
public class JerkIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Jerk? _jerk;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 4;
    public override string ShortName => "JERK";

    public JerkIndicator()
    {
        Name = "JERK - Third Derivative";
        Description = "Measures rate of change of acceleration - derivative of accel";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _jerk = new Jerk();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Jerk", Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_jerk == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _jerk.Update(input, isNew);

        bool isHot = _jerk.IsHot;

        LinesSeries[0].SetValue(_jerk.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double jerk = _jerk.Last.Value;
            Color color;
            if (jerk > 0)
                color = Color.Green;
            else if (jerk < 0)
                color = Color.Red;
            else
                color = Color.Gray;
            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(color));
        }
    }
}
