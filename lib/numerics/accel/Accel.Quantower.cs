using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// ACCEL (Second Derivative / Acceleration) Quantower indicator.
/// Measures the rate of change of the rate of change - derivative of slope.
/// </summary>
public class AccelIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Accel? _accel;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 3;
    public override string ShortName => "ACCEL";

    public AccelIndicator()
    {
        Name = "ACCEL - Second Derivative (Acceleration)";
        Description = "Measures rate of change of rate of change - derivative of slope";
        SeparateWindow = true;
        OnBackGround = false;
    }

    protected override void OnInit()
    {
        _accel = new Accel();
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("Accel", Momentum, 2, LineStyle.Histogramm));
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dot));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_accel == null || _selector == null) return;

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _accel.Update(input, isNew);

        bool isHot = _accel.IsHot;

        LinesSeries[0].SetValue(_accel.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0);

        if (isHot || ShowColdValues)
        {
            double accel = _accel.Last.Value;
            Color color;
            if (accel > 0)
                color = Color.Green;
            else if (accel < 0)
                color = Color.Red;
            else
                color = Color.Gray;
            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(color));
        }
    }
}
