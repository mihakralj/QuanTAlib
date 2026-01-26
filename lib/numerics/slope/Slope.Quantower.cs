using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// SLOPE (First Derivative / Velocity) Quantower indicator.
/// Measures the instantaneous rate of change between consecutive values.
/// </summary>
public class SlopeIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Slope? _slope;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 2;
    public override string ShortName => "SLOPE";

    public SlopeIndicator()
    {
        Name = "SLOPE - First Derivative (Velocity)";
        Description = "Measures instantaneous rate of change between consecutive values";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _slope = new Slope();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Slope", Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_slope == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _slope.Update(input, isNew);

        bool isHot = _slope.IsHot;

        LinesSeries[0].SetValue(_slope.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double slope = _slope.Last.Value;
            Color color;
            if (slope > 0)
            {
                color = Color.Green;
            }
            else if (slope < 0)
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
